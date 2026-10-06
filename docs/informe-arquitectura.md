# Informe de arquitectura — TP Inicial y Primera Parte

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
multicapa y el ejercicio de acceso local y remoto. La Primera Parte suma los patrones **Factory,
Repository, Strategy, Observer y Facade**, la fachada como única puerta de la presentación, los
eventos de dominio internos (`BusquedaCreada`) y el modelado con diagramas de capas y de secuencia.
El diagnóstico por IA, el servicio SOAP, la mensajería y los adaptadores a tiendas externas
corresponden a las entregas siguientes, y la arquitectura está preparada para recibirlos sin
rediseño (sección 7).

---

## 2. Componentes construidos

La consigna pide un mínimo de tres componentes reutilizables independientes. Se construyeron seis:

| # | Componente | Tipo pedido por la consigna | Responsabilidad |
|---|---|---|---|
| 1 | `RepMatch.Domain` | **Dominio** | Entidades, value objects, eventos de dominio e interfaces de repositorio |
| 2 | `RepMatch.Persistence` | **Acceso a datos** | `DbContext`, mapeos, repositorios y unidad de trabajo sobre EF Core |
| 3 | `RepMatch.Common` | **Utilidad** | Validación, logging, configuración externalizada, `ResultadoOperacion<T>`, medición de latencia y correlación |
| 4 | `RepMatch.Contracts` | Contratos | DTOs, `ICatalogoRepuestos` y `OpcionesCatalogo` |
| 5 | `RepMatch.Aplicacion` | Lógica de negocio | `FachadaAplicacion`, `ServicioClientes`, `ServicioBusquedas`, `DespachadorEventos`, `ObservadorCompatibilidad`, mapeadores, validadores y `CatalogoLocal` |
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
| Inventario | _(no aplica)_ | RepMatch no gestiona stock; ver la nota debajo de la tabla. |

**Sobre el Servicio de Inventario.** RepMatch no tiene inventario. El catálogo propio
(`IRepuestoRepository`) guarda las piezas y sus tablas de compatibilidad —qué repuesto le entra a
qué vehículo—, pero no cantidades ni precios. En la Segunda Parte, los adaptadores a tiendas
consultarán precio y disponibilidad en el momento de cada búsqueda, y cada resultado quedará
guardado como una `Oferta`: una foto del precio, el envío, la tienda y un indicador de
disponibilidad, con su fecha `CapturadaEn`. Es un registro de la búsqueda, no stock. Hoy `Oferta` y
`Busqueda.RegistrarOfertas` existen en el dominio, pero nadie las alimenta todavía. Mantener un
inventario propio implicaría almacenar datos de terceros, sincronizarlos continuamente y asumir la
responsabilidad de su exactitud, lo que contradice el modelo de negocio de un comparador. Si la
consigna menciona un "Servicio de Inventario" como ejemplo, en este dominio ese rol lo cumplirá el
catálogo de repuestos combinado con las consultas a las tiendas.

---

## 3. Arquitectura en capas

```
┌──────────────────────────────────────────────────────┐
│  PRESENTACIÓN    RepMatch.Web (Blazor Server)        │
│                  Componentes Razor                   │
└───────────────────────┬──────────────────────────────┘
                        │  entra por FachadaAplicacion
┌───────────────────────▼──────────────────────────────┐
│  LÓGICA DE       RepMatch.Aplicacion                 │
│  NEGOCIO         FachadaAplicacion · Servicios       │
│                  Observador · CatalogoLocal          │
│                  Validadores                         │
└───────────────────────┬──────────────────────────────┘
                        │  usa las interfaces declaradas en Domain
┌───────────────────────▼──────────────────────────────┐
│  DATOS           RepMatch.Persistence                │
│                  Repositorios · UnitOfWork · EF Core │
└───────────────────────┬──────────────────────────────┘
                        ▼
                 PostgreSQL / InMemory

   transversales:  RepMatch.Domain · RepMatch.Common · RepMatch.Contracts
```

Reglas que se respetan y que son verificables leyendo los `.csproj`:

- La presentación **nunca** referencia un repositorio: habla con `FachadaAplicacion`.
- La capa de datos **no contiene reglas de negocio**. `RepuestoRepository.BuscarCompatiblesAsync`
  filtra en la base por marca, modelo y rango de años —lo que el motor puede resolver con índice— y
  delega la regla fina de motorización a `Repuesto.EsCompatibleCon`, que vive en el dominio. Así la
  consulta es eficiente sin duplicar la lógica.
- La inversión de dependencias es real: `IRepuestoRepository` está declarada en `Domain` e
  implementada en `Persistence`, no al revés.

### Fachada como única puerta de la presentación (Primera Parte)

`FachadaAplicacion` es el único objeto que las páginas Razor inyectan de la capa de negocio. Agrupa
los métodos de `ServicioClientes`, `ServicioBusquedas` e `ICatalogoRepuestos` en una superficie
única y estable. Las páginas no conocen qué servicio interno maneja cada operación, ni si el
catálogo está en proceso o detrás de una API: esa decisión queda encapsulada. Además, `BuscarAsync`
es una operación compuesta que la fachada resuelve en un solo llamado —crear la búsqueda y resolver
las piezas compatibles—, coordinación que no le corresponde hacer a ninguna página. El observador ya
consultó el catálogo para decidir los códigos; la fachada lo vuelve a consultar para traer el detalle
de esas piezas.

### Despacho de eventos de dominio (Primera Parte)

Dentro de la capa de lógica de negocio, la resolución de piezas posterior a crear una búsqueda no
se hace por llamado directo sino por evento. Al crear una nueva `Busqueda`, `ServicioBusquedas`
copia los eventos acumulados por la entidad (`BusquedaCreada`), la persiste (`AgregarAsync` y
`ConfirmarAsync`), limpia los eventos de la entidad y recién después entrega la copia a
`DespachadorEventos`. `DespachadorEventos` itera todos los `IObservadorEventoDominio` registrados y
llama a los que aceptan ese tipo de evento. Hoy solo hay uno: `ObservadorCompatibilidad`, que
consulta el catálogo y asigna los códigos de pieza a la búsqueda en una segunda transacción.

El despacho ocurre **después** de que la búsqueda ya está persistida. Si no hay repuestos
compatibles, o si la consulta al catálogo lanza `HttpRequestException` o `TaskCanceledException`
(error HTTP o timeout del catálogo remoto), el observador marca la búsqueda como `Fallida` con un
motivo. Cualquier otra excepción —por ejemplo, un error de base de datos en modo Local— no se
atrapa: la búsqueda queda `Pendiente`, pero ya persistida. Ese error no se atrapa en el
despachador: se propaga a `ServicioBusquedas.CrearAsync`, que lo devuelve como
`ResultadoOperacion` fallido si es una `ExcepcionDominio`; cualquier otra excepción sube sin
atrapar. Y `IDespachadorEventos` es una interfaz: en la Segunda Parte se registrará otra
implementación de `IDespachadorEventos` que publique el evento a través de `IEventBus` (sobre
`RabbitMQ.Client`, sección 6.5), sin tocar `ServicioBusquedas`.

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
**Sin recompilar**, y sin que `FachadaAplicacion` ni las páginas se enteren de cuál quedó enchufada.

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

Deja en `docs/evidencias/` los logs de los tres procesos, la respuesta REST cruda, el contrato
OpenAPI y las dos mediciones de `GET /evidencia/catalogo`. En cada modo, el script hace 12 llamadas:
una primera (arranque en frío), diez de calentamiento y una última, que es la única que se guarda
en `06-medicion-local.json` o `07-medicion-remota.json`. La duración de las 12 queda en el log de la
Web (líneas `Evidencia acceso ... duracion=`).

### 5.2 Latencia medida

Consulta: repuestos compatibles con un **Volkswagen Gol 2015 1.6**. Los dos caminos devuelven
**los mismos 7 códigos**, en el mismo orden. Cada petición a `GET /evidencia/catalogo` crea un
ámbito nuevo y, por lo tanto, un `DbContext` nuevo. Valores de la corrida del 2026-10-05, tomados
de `01-web-modo-local.log` y `02-web-modo-remoto.log`:

| Ejecución | Local | Remoto | Sobrecosto |
|---|---|---|---|
| Primera llamada (arranque en frío) | 163,9 ms | 334,6 ms | +170,7 ms |
| Segunda llamada (todavía calentando) | 37,1 ms | 58,7 ms | +21,6 ms |
| **En régimen: llamadas 3 a 12, mediana (rango)** | **2,1 ms** (1,8–3,6) | **7,4 ms** (6,4–11,2) | **≈ +5 ms** |

`06-medicion-local.json` y `07-medicion-remota.json` guardan solo la última llamada de cada modo
(1,8 ms y 11,2 ms). Una sola muestra puede caer en cualquier punto del rango —en esta corrida, la
remota fue la más lenta de las diez—, por eso para comparar conviene mirar la mediana.

La primera llamada incluye la compilación JIT y la caché de consultas de EF Core, y en modo Remoto
además la apertura de la conexión HTTP; por eso no sirve para comparar los dos caminos. En régimen,
la invocación directa resuelve la consulta en un par de milisegundos y el salto por HTTP le suma
alrededor de **5 ms**. Los valores exactos cambian de una corrida a otra, pero el orden de magnitud
se mantiene. Ése es el precio de poder desplegar el componente de catálogo por separado.

### 5.3 Trazabilidad entre procesos

`CorrelacionMiddleware` asigna un `X-Correlation-Id` y `PropagacionCorrelacionHandler` lo copia a
las llamadas salientes. Una misma operación en modo Remoto queda así en los dos archivos de log:

```
# Web  (docs/evidencias/02-web-modo-remoto.log)
[19:57:11 INF] [Web] [c1be50fb134a] Sending HTTP request GET http://localhost:5081/api/repuestos/compatibles?*
[19:57:11 INF] [Web] [c1be50fb134a] Catalogo[Remoto] compatibilidad Volkswagen Gol 2015 (1.6) sistema=(todos) -> 7 repuestos
[19:57:11 INF] [Web] [c1be50fb134a] Evidencia acceso origen=Remoto duracion=11,2ms vehiculo=Volkswagen Gol 2015 (1.6) resultados=7

# Catalogo.Api  (docs/evidencias/03-catalogo-api.log)  ← mismo identificador
[19:57:11 INF] [Catalogo.Api] [c1be50fb134a] Catalogo[Local] compatibilidad Volkswagen Gol 2015 (1.6) sistema=(todos) -> 7 repuestos
[19:57:11 INF] [Catalogo.Api] [c1be50fb134a] HTTP GET /api/repuestos/compatibles responded 200 in 2.6876 ms
```

Con dos procesos ya hace falta; cuando en la Segunda Parte se sumen la cola, el servicio de IA y los
adaptadores externos, es lo único que permite reconstruir el recorrido completo de una búsqueda.

### 5.4 Verificación automatizada

`tests/RepMatch.Tests/Integracion/AccesoLocalVsRemotoTests.cs` es la versión ejecutable de esta
evidencia: hospeda `Catalogo.Api` en memoria con `WebApplicationFactory` y verifica que las dos
implementaciones devuelvan **exactamente lo mismo** para cinco vehículos distintos, para el listado
completo campo por campo, para códigos existentes e inexistentes y para el filtro por sistema.

```
Correctas! - Con error: 0, Superado: 79, Omitido: 0, Total: 79
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
| **Primera** | Patrones formalizados, fachada, eventos de dominio internos | `DespachadorEventos` publica `BusquedaCreada` a `ObservadorCompatibilidad`. La presentación entra por `FachadaAplicacion` | Dominio, contratos |
| **Segunda** | SOAP `ConsultaCompatibilidad` (CoreWCF + WSDL) | Tercera implementación de `ICatalogoRepuestos` | Presentación, lógica de negocio |
| **Segunda** | REST documentados con OpenAPI | Ya publicado en `/openapi/v1.json` | — |
| **Segunda** | RabbitMQ, productores y consumidores | Implementación de `IDespachadorEventos` sobre `IEventBus`; `BusquedaCreada` ya existe como evento de dominio | Entidades |
| **Segunda** | Adaptadores a eBay y VTEX | Nueva interfaz `IFuenteOfertas`, consumida por `search-api` | Catálogo, dominio |
| **Segunda** | Componente de IA (texto libre → repuestos) | Reemplaza o complementa a `ObservadorCompatibilidad` (que hoy asigna los códigos solo por compatibilidad de vehículo), leyendo `Busqueda.TextoLibre` | Contrato público del servicio |
| **Integrador** | Métricas de IA, pruebas de carga, despliegue | — | — |

El punto de extensión queda en el evento: `ServicioBusquedas.CrearAsync` persiste la búsqueda y
despacha `BusquedaCreada`. `ObservadorCompatibilidad` llena `CodigosObjetivo` consultando el
catálogo por compatibilidad de vehículo. El texto libre ya se guarda.

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

La evidencia de acceso local y remoto se genera con `bash scripts/evidencias.sh`.

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

## 10. Entregables de la Primera Parte

| Requisito de la consigna | Dónde está |
|---|---|
| Aplicación sobre framework empresarial | `src/RepMatch.Web`, `src/RepMatch.Catalogo.Api` |
| Patrones Factory, Repository, Strategy, Observer, Facade | [`docs/patrones.md`](patrones.md) |
| Servicios de negocio e integración por eventos | `ServicioClientes`, `ServicioBusquedas`, `ObservadorCompatibilidad` |
| Servicio de Inventario | No existe como módulo separado porque RepMatch no almacena stock. El catálogo interno cubre la compatibilidad de piezas; en la Segunda Parte, disponibilidad y precio se consultarán en el momento de cada búsqueda a las APIs de las tiendas. Justificación ampliada en la nota "Sobre el Servicio de Inventario" de la sección 2. |
| Diagrama de arquitectura en capas | [`docs/diagramas/capas.md`](diagramas/capas.md) |
| Diagramas de secuencia (búsqueda, login, alta de vehículo) | [`docs/diagramas/secuencia.md`](diagramas/secuencia.md) |
