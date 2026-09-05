# Informe de arquitectura de componentes — TP Inicial

**RepMatch** — Meta-buscador de repuestos automotrices con diagnóstico asistido por IA
Desarrollo de Aplicaciones II · UADE · Mg. Christian Parkinson

---

## 1. La aplicación

RepMatch es un **comparador de ofertas de repuestos**, no un e-shop ni un sistema de gestión de
stock. El recorrido completo, cuando esté terminado, es:

1. El usuario identifica su vehículo (marca/modelo/año, o pegando el VIN).
2. Describe el problema **en lenguaje natural**: *"cuando freno vibra el volante y chilla"*.
3. El componente de IA propone un diagnóstico, un nivel de urgencia y los repuestos candidatos.
4. La aplicación consulta **varios sitios reales en paralelo**, normaliza las ofertas y las compara
   por precio total, disponibilidad y tienda.
5. El usuario elige y **se va al sitio del vendedor**. RepMatch no cobra, no reserva ni almacena
   stock.

Ese último punto es deliberado: es lo que mantiene la aplicación fuera del e-shop que el docente
descartó, y lo que la convierte en un problema genuino de **integración de aplicaciones** —que es el
eje de la materia— en vez de un ABM con carrito.

### Alcance de esta entrega

El TP Inicial cubre la **arquitectura de componentes**: los componentes reutilizables, la aplicación
multicapa y el ejercicio de acceso local y remoto. El diagnóstico por IA, el servicio SOAP, la
mensajería y los adaptadores a tiendas externas corresponden a las entregas siguientes, y la
arquitectura está preparada para recibirlos sin rediseño (sección 7).

---

## 2. Componentes construidos

La consigna pide un mínimo de tres componentes reutilizables independientes. Se construyeron seis:

| # | Componente | Tipo pedido por la consigna | Responsabilidad |
|---|---|---|---|
| 1 | `RepMatch.Domain` | **Dominio** | Entidades, value objects, eventos de dominio e interfaces de repositorio |
| 2 | `RepMatch.Persistence` | **Acceso a datos** | `DbContext`, mapeos, repositorios y unidad de trabajo sobre EF Core |
| 3 | `RepMatch.Common` | **Utilidad** | Validación, logging, configuración externalizada, `ResultadoOperacion<T>`, medición de latencia y correlación |
| 4 | `RepMatch.Contracts` | Contratos | DTOs, `ICatalogoRepuestos` y `OpcionesCatalogo` |
| 5 | `RepMatch.Aplicacion` | Lógica de negocio | `ServicioClientes`, `ServicioBusquedas`, mapeadores, validadores y `CatalogoLocal` |
| 6 | `RepMatch.Clientes.Rest` | Adaptador remoto | `CatalogoRemoto` y propagación de correlación |

Más dos hosts ejecutables: `RepMatch.Web` (presentación) y `RepMatch.Catalogo.Api` (host REST del
componente de catálogo).

**Criterio de independencia.** `Domain`, `Common` y `Contracts` no referencian ningún otro proyecto
de la solución. Ésa es la prueba de que son reutilizables y no fragmentos de una misma bola de barro.
`Domain` en particular no conoce EF Core, ASP.NET ni ningún atributo de persistencia: todo el mapeo
relacional vive en `Persistence/Configuraciones/`.

### Mapeo con el dominio que pide la consigna

La consigna nombra `Pedido`, `Cliente` y `Producto`. En un comparador esos conceptos se
materializan así:

| Consigna | RepMatch | Justificación |
|---|---|---|
| Cliente | `Cliente` | Igual: quien busca, con su garage de vehículos. |
| Producto | `Repuesto` + `Oferta` | `Repuesto` es la pieza como concepto (código canónico, equivalencias, tabla de compatibilidad). `Oferta` es esa pieza publicada por una tienda a un precio. **Separarlos es lo que hace posible comparar la misma pieza entre sitios distintos**, que es el corazón de la aplicación. |
| Pedido | `Busqueda` | El agregado que atraviesa el sistema: vehículo + texto libre + estado + resultados. Cumple el mismo rol estructural que un pedido, pero lo que se "despacha" es la consulta a las tiendas. |

---

## 3. Arquitectura en capas

```
┌──────────────────────────────────────────────────────┐
│  PRESENTACIÓN    RepMatch.Web (Blazor Server)        │
│                  Componentes Razor · FabricaCatalogo  │
└───────────────────────┬──────────────────────────────┘
                        │  depende solo de ICatalogoRepuestos
┌───────────────────────▼──────────────────────────────┐
│  LÓGICA DE       RepMatch.Aplicacion                 │
│  NEGOCIO         ServicioClientes · ServicioBusquedas │
│                  CatalogoLocal · Validadores          │
└───────────────────────┬──────────────────────────────┘
                        │  usa las interfaces declaradas en Domain
┌───────────────────────▼──────────────────────────────┐
│  DATOS           RepMatch.Persistence                │
│                  Repositorios · UnitOfWork · EF Core  │
└───────────────────────┬──────────────────────────────┘
                        ▼
                 PostgreSQL / InMemory

   transversales:  RepMatch.Domain · RepMatch.Common · RepMatch.Contracts
```

Reglas que se respetan y que son verificables leyendo los `.csproj`:

- La presentación **nunca** referencia un repositorio: habla con `ServicioClientes` y
  `ServicioBusquedas`.
- La capa de datos **no contiene reglas de negocio**. `RepuestoRepository.BuscarCompatiblesAsync`
  filtra en la base por marca, modelo y rango de años —lo que el motor puede resolver con índice— y
  delega la regla fina de motorización a `Repuesto.EsCompatibleCon`, que vive en el dominio. Así la
  consulta es eficiente sin duplicar la lógica.
- La inversión de dependencias es real: `IRepuestoRepository` está declarada en `Domain` e
  implementada en `Persistence`, no al revés.

---

## 4. Acceso local vs. remoto entre componentes

Es el ejercicio central del TP Inicial. La solución adoptada es **una interfaz con dos
implementaciones intercambiables por configuración**.

### El contrato

```csharp
// RepMatch.Contracts/ICatalogoRepuestos.cs
public interface ICatalogoRepuestos
{
    string Modo { get; }   // "Local" | "Remoto" — se registra en cada log
    Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(VehiculoDto v, string? sistema, CancellationToken ct);
    Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct);
    Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct);
}
```

### Las dos implementaciones

| | **Local** | **Remoto** |
|---|---|---|
| Clase | `Aplicacion.Catalogo.CatalogoLocal` | `Clientes.Rest.CatalogoRemoto` |
| Mecanismo | Invocación directa en proceso, por referencia de proyecto | HTTP + JSON contra `Catalogo.Api` |
| Recorrido | `CatalogoLocal → IRepuestoRepository → EF Core → BD` | `HttpClient → GET /api/repuestos/compatibles → CatalogoLocal (otro proceso) → BD` |
| Serialización | ninguna | JSON de ida y vuelta |
| Frontera de fallas | excepciones del dominio | además: timeouts, DNS, códigos HTTP |

### La selección

`RepMatch.Web/Servicios/FabricaCatalogo.cs` (patrón **Factory**) lee la configuración y enlaza la
interfaz:

```csharp
servicios.AddScoped<ICatalogoRepuestos>(sp => opciones.Modo switch
{
    ModoAccesoCatalogo.Remoto => sp.GetRequiredService<CatalogoRemoto>(),
    _                         => sp.GetRequiredService<CatalogoLocal>()
});
```

Se cambia con `Catalogo:Modo` en `appsettings.json` o con la variable de entorno `Catalogo__Modo`.
**Sin recompilar**, y sin que ningún consumidor —ni los componentes Razor, ni `ServicioBusquedas`—
se entere de cuál quedó enchufada.

### Por qué esto no es una complicación gratuita

La misma decisión resuelve un problema real de las entregas siguientes: cuando el catálogo pase a
ser un microservicio propio y aparezca además una tercera vía SOAP (CoreWCF), sólo hay que agregar
otra implementación de la misma interfaz. La presentación no se toca.

---

## 5. Evidencias de ejecución

### 5.1 Reproducirlas

```bash
bash scripts/evidencias.sh
```

Deja en `docs/evidencias/` los logs de los tres procesos, la respuesta REST cruda y el contrato
OpenAPI. Para la evidencia visual, `http://localhost:5080/acceso` ejecuta **los dos caminos a la vez**
y muestra ambas latencias juntas en una sola pantalla.

### 5.2 Latencia medida

Consulta: repuestos compatibles con un **Volkswagen Gol 2015 1.6**. Los dos caminos devuelven
**los mismos 7 códigos**, en el mismo orden.

Medido desde la página `/acceso`, que reutiliza el ámbito de inyección del circuito Blazor:

| Ejecución | Local | Remoto | Sobrecosto |
|---|---|---|---|
| Primera (arranque en frío) | 236,7 ms | 456,6 ms | +219,8 ms |
| **En régimen** | **1,9 ms** | **12,6 ms** | **+10,7 ms** |

Medido desde el endpoint `/evidencia/catalogo`, donde **cada petición crea un ámbito nuevo** y por
lo tanto un `DbContext` nuevo:

| Ejecución | Local | Remoto | Sobrecosto |
|---|---|---|---|
| Primera | 253,6 ms | 584,8 ms | +331,2 ms |
| Segunda | 66,5 ms | 81,1 ms | +14,6 ms |

Las dos mediciones son legítimas y la diferencia entre ellas es informativa: en el circuito Blazor el
acceso directo cuesta **~6,6 veces menos** que el remoto porque el ámbito está caliente y la consulta
ya está compilada; por HTTP, el costo de levantar un `DbContext` nuevo en cada petición domina y
achica la brecha relativa. En ambos casos el sobrecosto absoluto del salto remoto ronda los
**10–15 ms**, y ése es el precio que se paga por poder desplegar el componente por separado.

### 5.3 Trazabilidad entre procesos

`CorrelacionMiddleware` asigna un `X-Correlation-Id` y `PropagacionCorrelacionHandler` lo copia a
las llamadas salientes. Una misma operación en modo Remoto queda así en los dos archivos de log:

```
# Web  (docs/evidencias/02-web-modo-remoto.log)
[10:27:11 INF] [Web] [5d7dce6bcbbf] Sending HTTP request GET http://localhost:5081/api/repuestos/compatibles?*
[10:27:11 INF] [Web] [5d7dce6bcbbf] Catalogo[Remoto] compatibilidad Volkswagen Gol 2015 (1.6) -> 7 repuestos
[10:27:11 INF] [Web] [5d7dce6bcbbf] Evidencia acceso origen=Remoto duracion=79,7ms resultados=7

# Catalogo.Api  (docs/evidencias/03-catalogo-api.log)  ← mismo identificador
[10:27:11 INF] [Catalogo.Api] [5d7dce6bcbbf] Catalogo[Local] compatibilidad Volkswagen Gol 2015 (1.6) -> 7 repuestos
[10:27:11 INF] [Catalogo.Api] [5d7dce6bcbbf] HTTP GET /api/repuestos/compatibles responded 200 in 71.2007 ms
```

Con dos procesos ya hace falta; cuando en la Segunda Parte se sumen la cola, el servicio de IA y los
adaptadores externos, es lo único que permite reconstruir el recorrido completo de una búsqueda.

### 5.4 Verificación automatizada

`tests/RepMatch.Tests/Integracion/AccesoLocalVsRemotoTests.cs` es la versión ejecutable de esta
evidencia: hospeda `Catalogo.Api` en memoria con `WebApplicationFactory` y verifica que las dos
implementaciones devuelvan **exactamente lo mismo** para cinco vehículos distintos, para el listado
completo campo por campo, para códigos existentes e inexistentes y para el filtro por sistema.

```
Correctas! - Con error: 0, Superado: 54, Omitido: 0, Total: 54
```

Si algún día las dos implementaciones divergen, esto falla antes que la demo.

---

## 6. Decisiones de diseño y sus fundamentos

### 6.1 Fuentes de ofertas: APIs públicas oficiales, no scraping

Se relevaron las fuentes reales disponibles antes de comprometer el diseño:

| Fuente | Resultado | Decisión |
|---|---|---|
| MercadoLibre `sites/MLA/search` | **HTTP 403 forbidden** sin token — cerraron la búsqueda pública | ❌ Descartada |
| `argautopartes.com.ar` (Tiendanube) | `robots.txt` con `Disallow: /search/` | ❌ Descartada |
| **VTEX `easy.com.ar`** | HTTP 206 con autopartes reales y precio, endpoint público, permitido por `robots.txt` | ✅ Adoptada |
| **VTEX `fravega.com`** | HTTP 206, mismo contrato | ✅ Adoptada |
| **eBay Browse API** | API oficial, OAuth2 client_credentials, 5000 llamadas/día, filtros de compatibilidad vehicular | ✅ Adoptada |
| **NHTSA vPIC** | HTTP 200, sin API key, sin rate limit | ✅ Adoptada |

La conclusión que gobierna el diseño: **usar endpoints públicos y documentados en vez de parsear
HTML**. VTEX expone `/api/catalog_system/pub/products/search?ft=` sin autenticación, y como es una
plataforma compartida, *un solo adaptador parametrizado por `baseUrl` sirve para todas las tiendas
que corran sobre ella*. Es más estable que un scraper, no viola `robots.txt`, y es defendible en la
exposición oral.

### 6.2 `Dinero` como value object

El comparador mezcla ofertas en ARS (tiendas VTEX) y USD (eBay). Sumar o comparar importes de
distinta moneda lanza `ExcepcionDominio` en vez de producir un número silenciosamente incorrecto.
Y `Oferta.PrecioTotal` incluye el envío: ordenar por precio de lista haría ganar el ranking a una
oferta barata con envío carísimo.

### 6.3 `ResultadoOperacion<T>` en vez de excepciones para errores esperables

Un email duplicado o un vehículo sin repuestos catalogados no son situaciones excepcionales: son
resultados normales que la interfaz tiene que mostrar. Las excepciones quedan reservadas para lo que
de verdad no debería pasar. Acceder a `.Valor` de un resultado fallido lanza, porque eso sí es un
bug del llamador.

### 6.4 Proveedor de datos conmutable

`Persistencia:Proveedor` acepta `InMemory` o `Postgres`. El primero permite correr, demostrar y
testear la aplicación **sin Docker ni base instalada**; el segundo es el del entorno containerizado.
La decisión se toma en un único punto (`ExtensionesPersistencia`) y ningún repositorio se entera.

### 6.5 RabbitMQ.Client directo, no MassTransit *(decisión anticipada para la Segunda Parte)*

MassTransit 9 pasó a licencia comercial y la rama 8 (Apache-2.0) queda sin soporte a fin de 2026.
Más allá de la licencia: la consigna evalúa *"implementación de productores y consumidores de
mensajes"*, y escribirlos explícitamente sobre `RabbitMQ.Client` 7.2.2 muestra la mecánica en vez de
esconderla detrás de un framework. Se envolverá en una interfaz `IEventBus` propia.

### 6.6 Versiones y seguridad

La plantilla de `webapi` arrastraba `Microsoft.OpenApi` 2.0.0, afectado por **CVE-2026-49451**
(denegación de servicio por recursión no controlada al parsear documentos OpenAPI con referencias
circulares, CVSS 7.5). Se fijó la versión **2.12.2**, posterior al parche 2.7.5, y se alineó
`Microsoft.AspNetCore.OpenApi` en 10.0.11. La solución compila **sin advertencias**.

---

## 7. Cómo esta arquitectura recibe las entregas siguientes

| Entrega | Qué agrega | Dónde se enchufa | Qué NO hay que tocar |
|---|---|---|---|
| **Primera** | Patrones formalizados, servicios de negocio, eventos de dominio internos | `EntidadBase.EventosDominio` ya acumula eventos; falta el despachador | Dominio, contratos |
| **Segunda** | SOAP `ConsultaCompatibilidad` (CoreWCF + WSDL) | Tercera implementación de `ICatalogoRepuestos` | Presentación, lógica de negocio |
| **Segunda** | REST documentados con OpenAPI | Ya publicado en `/openapi/v1.json` | — |
| **Segunda** | RabbitMQ, productores y consumidores | `BusquedaCreada` ya existe como evento de dominio | Entidades |
| **Segunda** | Adaptadores a eBay y VTEX | Nueva interfaz `IFuenteOfertas`, consumida por `search-api` | Catálogo, dominio |
| **Segunda** | Componente de IA (texto libre → repuestos) | Reemplaza el paso de `ServicioBusquedas.CrearAsync` que hoy asigna códigos por compatibilidad | Contrato público del servicio |
| **Integrador** | Métricas de IA, pruebas de carga, despliegue | — | — |

El punto de extensión de la IA ya está aislado: hoy `ServicioBusquedas.CrearAsync` llena
`CodigosObjetivo` consultando el catálogo por compatibilidad; mañana los llena el servicio de
diagnóstico a partir de `Busqueda.TextoLibre` —que ya se persiste, aunque todavía no se procese—.
La firma pública del servicio no cambia.

---

## 8. Cómo ejecutar

### Sin Docker

```bash
dotnet build && dotnet test
```

```bash
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
```

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

Abrir `http://localhost:5080/acceso`.

### Con Docker

```bash
docker compose up --build
```

Web en `http://localhost:8080`, Catálogo en `http://localhost:8081/salud`, PostgreSQL en `5432`.
En compose la Web arranca en modo **Remoto**, de modo que el acceso remoto entre componentes queda
demostrado apenas se levanta el entorno.

---

## 9. Entregables del TP Inicial

| Requisito de la consigna | Dónde está |
|---|---|
| Entorno configurado (.NET 10, contenedores) | `docker-compose.yml`, `src/*/Dockerfile` |
| ≥3 componentes reutilizables independientes | 6 componentes — sección 2 |
| Aplicación multicapa | Sección 3 |
| Gestión de dependencias (NuGet) | `RepMatch.slnx`, `*.csproj` |
| Acceso local y remoto entre componentes | Sección 4 |
| Diagrama de clases | [`docs/diagramas/clases.md`](diagramas/clases.md) |
| Diagrama de componentes | [`docs/diagramas/componentes.md`](diagramas/componentes.md) |
| Diagrama de despliegue | [`docs/diagramas/despliegue.md`](diagramas/despliegue.md) |
| Código fuente | `src/`, `tests/` |
| Informe de arquitectura | este documento |
| Evidencias de ejecución local y remota | `docs/evidencias/` + `scripts/evidencias.sh` + sección 5 |
