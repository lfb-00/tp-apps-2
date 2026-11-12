# Informe de arquitectura — TP Inicial y Primera Parte

**RepMatch** — Meta-buscador de repuestos automotrices con diagnóstico asistido por IA
Desarrollo de Aplicaciones II · UADE · Mg. Christian Parkinson

---

## 1. La aplicación

RepMatch es un **comparador de ofertas de repuestos**, no un e-shop ni un sistema de gestión de
stock. El recorrido completo, cuando esté terminado, es:

1. El usuario identifica su vehículo (marca/modelo/año, o pegando el VIN).
2. Describe el problema **en lenguaje natural**: *"cuando freno vibra el volante y chilla"*.
3. El componente de IA propone un diagnóstico y los repuestos candidatos.
4. La aplicación consulta **varios sitios reales en paralelo**, normaliza las ofertas y las compara
   por precio total, disponibilidad y tienda.
5. El usuario elige y **se va al sitio del vendedor**. RepMatch no cobra, no reserva ni almacena
   stock.

Ese último punto es deliberado: es lo que mantiene la aplicación fuera del e-shop que el docente
descartó, y lo que la convierte en un problema genuino de **integración de aplicaciones** —que es el
eje de la materia— en vez de un ABM con carrito.

### Roles y modelo de negocio

Hay dos roles, que se eligen al crear la cuenta:

- **Cliente.** Busca piezas para su auto y puede compartir una consulta con un mecánico del
  directorio para que revise el diagnóstico. Nunca paga.
- **Mecánico.** También tiene garage y busca, pero además aparece en el directorio con su taller,
  zona y especialidades, recibe en una bandeja las consultas que le comparten y las revisa:
  confirma las piezas propuestas o recomienda otras, con una nota para el cliente.

Como RepMatch no vende ni cobra la transacción, el ingreso sale de dos lugares que no lo
contradicen:

1. **Comisión de afiliado.** Cada "Ir a la tienda" pasa por `GET /derivar/{ofertaId}`, que registra
   una `Derivacion` y redirige al sitio de origen. La tienda paga por las derivaciones que terminan
   en compra. Si la oferta es de una pieza que recomendó el mecánico que revisó la consulta, la
   derivación se le atribuye: el mecánico es el canal que decide qué se compra.
2. **Plan Pro del mecánico (freemium).** El plan gratuito admite hasta 5 consultas sin revisar en la
   bandeja; el Pro no tiene límite y aparece primero en el directorio. El cambio de plan no cobra:
   en producción el pago lo procesaría un proveedor externo y RepMatch solo aplica los límites.

La regla que sostiene el modelo: **el orden de las ofertas depende solo del precio total**, nunca de
quién paga. La comisión no altera el ranking, y la recomendación del mecánico se muestra como
recomendación suya, no como resultado patrocinado. El "perfil destacado" del Pro ordena el
directorio de mecánicos, no las ofertas.

### Alcance de esta entrega

El TP Inicial cubre la **arquitectura de componentes**: los componentes reutilizables, la aplicación
multicapa y el ejercicio de acceso local y remoto. La Primera Parte suma los patrones **Factory,
Repository, Strategy, Observer y Facade**, la fachada para las operaciones de clientes, búsquedas y repuestos, los
eventos de dominio internos (`BusquedaCreada`) y el modelado con diagramas de capas y de secuencia.
Sobre esa base se agregaron los roles cliente y mecánico, la revisión del diagnóstico y el registro
de derivaciones del modelo de negocio.
De la Segunda Parte ya están el **componente de IA** (diagnóstico del texto libre, expuesto por REST
en `RepMatch.IA.Api`, secciones 6.7 y 7) y el **servicio SOAP** del catálogo (`RepMatch.Catalogo.Soap`,
con WSDL, secciones 4 y 6.8), que la Web usa en cada búsqueda, y la **API externa real**: el
Servicio de Ofertas consulta los precios en Easy y Frávega con la API pública de VTEX
(`RepMatch.Fuentes.Vtex`, sección 6.9). También está la **cola de mensajes** con productores y
consumidores: los eventos de dominio se publican en RabbitMQ y un worker los consume (sección
6.10). El adaptador de eBay y las notificaciones a cliente y mecánico corresponden a las entregas
siguientes, y la arquitectura está preparada para recibirlos sin rediseño (sección 7).

---

## 2. Componentes construidos

La consigna pide un mínimo de tres componentes reutilizables independientes. Se construyeron diez
(los seis del TP Inicial y, de la Segunda Parte, el de IA, el cliente SOAP, el adaptador de tiendas
VTEX y el de mensajería):

| # | Componente | Tipo pedido por la consigna | Responsabilidad |
|---|---|---|---|
| 1 | `RepMatch.Domain` | **Dominio** | Entidades, value objects, eventos de dominio e interfaces de repositorio |
| 2 | `RepMatch.Persistence` | **Acceso a datos** | `DbContext`, mapeos, migraciones, repositorios y unidad de trabajo sobre EF Core |
| 3 | `RepMatch.Common` | **Utilidad** | Validación, logging, configuración externalizada, `ResultadoOperacion<T>`, medición de latencia y correlación |
| 4 | `RepMatch.Contracts` | Contratos | DTOs, `ICatalogoRepuestos`, `ICatalogoVehiculos`, `IDiagnosticador`, `IFuenteOfertas`, `IEventBus` y los eventos de integración, `OpcionesCatalogo`, `OpcionesIA`, `OpcionesOfertas` y `OpcionesMensajeria` |
| 5 | `RepMatch.Aplicacion` | Lógica de negocio | `FachadaAplicacion`, `ServicioClientes`, `ServicioBusquedas`, `ServicioMecanicos`, `ServicioRevisiones`, `ServicioDerivaciones`, `ServicioDiagnostico`, `ServicioOfertas`, `ServicioComisiones`, `DespachadorEventos`, `ObservadorCompatibilidad`, `PublicadorEventos`, mapeadores, validadores y los catálogos locales de repuestos y vehículos |
| 6 | `RepMatch.Clientes.Rest` | Adaptador remoto | `CatalogoRemoto`, `CatalogoVehiculosRemoto`, `DiagnosticadorRemoto` y propagación de correlación |
| 7 | `RepMatch.IA` | Componente de IA | `DiagnosticadorLlm` (adapter del LLM), `DiagnosticadorLocal` (clasificador TF-IDF + k-NN), `DiagnosticadorConRespaldo`, filtro de datos personales y el corpus de síntomas |
| 8 | `RepMatch.Clientes.Soap` | Adaptador SOAP | `CatalogoSoap`, el proxy generado con `dotnet-svcutil` desde el WSDL de `Catalogo.Soap` y la propagación de correlación |
| 9 | `RepMatch.Fuentes.Vtex` | Adaptador de API externa | `FuenteVtex` (una instancia por tienda configurada), `LectorVtex` (traduce la respuesta del buscador de VTEX) y su registro `AgregarFuentesOfertas` |
| 10 | `RepMatch.Mensajeria` | Mensajería | `BusRabbitMq` (implementa `IEventBus`), `ConexionRabbitMq`, la topología (`TopologiaRabbitMq`) y la base de los consumidores (`ConsumidorRabbitMq<T>`), sobre `RabbitMQ.Client` |

Más cinco hosts ejecutables: `RepMatch.Web` (presentación), `RepMatch.Catalogo.Api` (host REST del
componente de catálogo), `RepMatch.Catalogo.Soap` (host SOAP del mismo componente, con CoreWCF),
`RepMatch.IA.Api` (host REST del componente de IA, sin base de datos) y
`RepMatch.Notificaciones.Worker` (consumidor de la cola, con `/salud` y `GET /api/comisiones`).

**Criterio de independencia.** `Domain`, `Common` y `Contracts` no referencian ningún otro proyecto
de la solución. Ésa es la prueba de que son reutilizables y no fragmentos de una misma bola de barro.
`Domain` en particular no conoce EF Core, ASP.NET ni ningún atributo de persistencia: todo el mapeo
relacional vive en `Persistence/Configuraciones/`.

### Mapeo con el dominio que pide la consigna

La consigna nombra `Pedido`, `Cliente` y `Producto`. En un comparador esos conceptos se
materializan así:

| Consigna | RepMatch | Justificación |
|---|---|---|
| Cliente | `Cliente` | Igual: quien busca, con su garage de vehículos. La misma entidad representa al mecánico, con `Rol = Mecanico`, datos de taller y plan. |
| Producto | `Repuesto` + `Oferta` | `Repuesto` es la pieza como concepto (código canónico, equivalencias, tabla de compatibilidad). `Oferta` es esa pieza publicada por una tienda a un precio. **Separarlos es lo que hace posible comparar la misma pieza entre sitios distintos**, que es el corazón de la aplicación. |
| Pedido | `Busqueda` | El agregado que atraviesa el sistema: vehículo + texto libre + estado + resultados. Cumple el mismo rol estructural que un pedido, pero lo que se "despacha" es la consulta a las tiendas. |
| Inventario | `ServicioOfertas` | RepMatch no tiene stock propio: su "inventario" son las ofertas de las tiendas. Ver la nota debajo de la tabla. |

**Sobre el Servicio de Inventario.** RepMatch no tiene inventario propio. El catálogo
(`IRepuestoRepository`) guarda las piezas y sus tablas de compatibilidad —qué repuesto le entra a
qué vehículo—, pero no cantidades ni precios. El equivalente es `ServicioOfertas`: consulta precio y
disponibilidad en las tiendas en el momento de la búsqueda y guarda cada resultado como una
`Oferta`, una foto del precio, la tienda, el vendedor y la disponibilidad, con su fecha
`CapturadaEn`. Con eso hace lo que haría un servicio de inventario sobre datos de terceros: no
duplica publicaciones (la misma publicación de la misma tienda se actualiza), marca como no
disponible lo que una tienda dejó de publicar, vuelve a consultar cuando los precios vencen
(`Ofertas:VigenciaMinutos`) y arma el ranking por precio total sin mezclar monedas. Mantener stock
propio implicaría almacenar datos de terceros, sincronizarlos todo el tiempo y responder por su
exactitud, lo que contradice el modelo de negocio de un comparador.

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

Las referencias de proyectos y el uso de sus tipos muestran estas decisiones:

- La presentación no llama repositorios: habla con `FachadaAplicacion` y, para los selectores,
  con `ICatalogoVehiculos`.
- La capa de datos **no contiene reglas de negocio**. `RepuestoRepository.BuscarCompatiblesAsync`
  filtra en la base por marca, modelo y rango de años —el filtro que EF traduce a SQL— y
  delega la regla fina de motorización a `Repuesto.EsCompatibleCon`, que vive en el dominio. Así la
  consulta es eficiente sin duplicar la lógica.
- La inversión de dependencias es real: `IRepuestoRepository` está declarada en `Domain` e
  implementada en `Persistence`, no al revés.

### Fachada y consulta de opciones vehiculares (Primera Parte)

Las páginas usan `FachadaAplicacion` para clientes, mecánicos, búsquedas y repuestos. Agrupa
los métodos de `ServicioClientes`, `ServicioBusquedas`, `ServicioMecanicos`, `ServicioRevisiones`,
`ServicioDerivaciones`, `ServicioDiagnostico`, `ServicioOfertas`, `ICatalogoRepuestos` e
`ICatalogoVehiculos` en una superficie única y estable. Los permisos por rol se chequean en esos servicios —quién puede ver una consulta,
compartirla o revisarla, y el límite del plan—, no en las páginas: la presentación solo decide qué
mostrar. Las páginas no conocen qué servicio interno maneja cada operación, ni si el
catálogo está en proceso o detrás de una API: esa decisión queda encapsulada. Además, `BuscarAsync`
es una operación compuesta que la fachada resuelve en un solo llamado —crear la búsqueda y resolver
las piezas compatibles—, coordinación que no le corresponde hacer a ninguna página. El observador ya
consultó el catálogo para decidir los códigos; la fachada lo vuelve a consultar para traer el detalle
de esas piezas. La búsqueda sin sesión usa `DiagnosticarAsync`, que valida el vehículo y devuelve
diagnóstico y piezas sin crear una `Busqueda`.

Los precios llegan en un segundo paso, a propósito. La página muestra el diagnóstico y las piezas
apenas vuelve `BuscarAsync`, con "Buscando precios…" en cada pieza, y recién entonces pide
`ObtenerOfertasAsync`: una tienda lenta no demora el diagnóstico. Es también el comportamiento que va
a tener con la cola de mensajes, cuando el consumidor de `BusquedaCreada` sea quien busque los
precios y la página solo los lea. Sin sesión, `ResumirPreciosAsync` devuelve "desde cuánto y en
cuántas tiendas" sin guardar nada: ver dónde y derivar requiere una búsqueda guardada, que es de
donde sale la comisión.

`SelectorVehiculo.razor` inyecta directamente `ICatalogoVehiculos` para obtener las opciones.
La fábrica selecciona este contrato entre `CatalogoVehiculosLocal` y `CatalogoVehiculosRemoto`.
`BuscarAsync` y `BuscarRepuestosAsync` validan la configuración antes de consultar piezas.

### Despacho de eventos de dominio (Primera Parte)

Dentro de la capa de lógica de negocio, la resolución de piezas posterior a crear una búsqueda no
se hace por llamado directo sino por evento. Al crear una nueva `Busqueda`, `ServicioBusquedas`
copia los eventos acumulados por la entidad (`BusquedaCreada`), la persiste (`AgregarAsync` y
`ConfirmarAsync`), limpia los eventos de la entidad y recién después entrega la copia a
`DespachadorEventos`. `DespachadorEventos` itera todos los `IObservadorEventoDominio` registrados y
llama a los que aceptan ese tipo de evento. Hoy solo hay uno: `ObservadorCompatibilidad`, que
consulta el catálogo y asigna los códigos de pieza a la búsqueda en una segunda transacción. Desde
la Segunda Parte lo hace a través de `ServicioDiagnostico`: la IA lee el texto libre y elige entre
las piezas compatibles, y el observador guarda también el diagnóstico (sistema, urgencia, causas y
fuente). Si la IA no responde (`HttpRequestException` o `TaskCanceledException` del modo remoto),
`ServicioDiagnostico` lo registra y quedan todas las compatibles, como en la Primera Parte.

El despacho ocurre **después** de que la búsqueda ya está persistida. Si no hay repuestos
compatibles, o si la consulta al catálogo lanza `HttpRequestException` o `TaskCanceledException`
(error HTTP o timeout del catálogo remoto), el observador marca la búsqueda como `Fallida` con un
motivo. Cualquier otra excepción —por ejemplo, un error de base de datos en modo Local— no se
atrapa: la búsqueda queda `Pendiente`, pero ya persistida. Ese error no se atrapa en el
despachador: se propaga a `ServicioBusquedas.CrearAsync`, que lo devuelve como
`ResultadoOperacion` fallido si es una `ExcepcionDominio`; cualquier otra excepción sube sin
atrapar.

`ServicioRevisiones` y `ServicioDerivaciones` siguen el mismo orden —confirmar y después
despachar— con `BusquedaCompartida`, `DiagnosticoValidado` y `DerivacionATienda`.

En la Segunda Parte el despacho quedó **compuesto** sin cambiar `ServicioBusquedas` ni el
despachador: se registró un segundo observador, `PublicadorEventos`, que acepta los cuatro eventos
y los publica en la cola por `IEventBus` (sección 6.10). Va registrado después de
`ObservadorCompatibilidad`, y el despachador los llama en ese orden: primero se completa la
búsqueda en proceso, porque la pantalla necesita las piezas ya, y después se publica. Si el broker
no responde, el observador lo registra y la operación sigue.

---

## 4. Acceso local vs. remoto entre componentes

Es el ejercicio central del TP Inicial. Cada contrato, `ICatalogoRepuestos` e `ICatalogoVehiculos`, tiene
**dos implementaciones intercambiables por configuración**. En la Segunda Parte, el de repuestos sumó
una **tercera, por SOAP**, sin cambiar la interfaz ni sus consumidores. A continuación se muestra el de
repuestos.

### El contrato

Ver [ICatalogoRepuestos.cs](../src/RepMatch.Contracts/ICatalogoRepuestos.cs).

### Las implementaciones

| | **Local** | **Remoto** | **SOAP** *(Segunda Parte)* |
|---|---|---|---|
| Clase | `Aplicacion.Catalogo.CatalogoLocal` | `Clientes.Rest.CatalogoRemoto` | `Clientes.Soap.CatalogoSoap` |
| Mecanismo | Invocación directa en proceso, por referencia de proyecto | HTTP + JSON contra `Catalogo.Api` | Sobre SOAP 1.1 por HTTP contra `Catalogo.Soap`, con el proxy generado desde el WSDL |
| Recorrido | `CatalogoLocal → IRepuestoRepository → EF Core → BD` | `HttpClient → GET /api/repuestos/compatibles → CatalogoLocal (otro proceso) → BD` | `canal WCF → POST /ConsultaCompatibilidad.svc (ConsultarPorVehiculo) → CatalogoLocal (otro proceso) → BD` |
| Serialización | ninguna | query string de entrada y JSON de respuesta | XML en los dos sentidos, descripto por el WSDL |
| Frontera de fallas | excepciones del dominio | además: timeouts, DNS, códigos HTTP | además: faults SOAP; el adapter las traduce a `HttpRequestException` |

### La selección

`RepMatch.Web/Servicios/FabricaCatalogo.cs` (patrón **Factory**) lee la configuración y enlaza la
interfaz:

Ver [FabricaCatalogo.cs](../src/RepMatch.Web/Servicios/FabricaCatalogo.cs).

Se cambia con `Catalogo:Modo` en `appsettings.json` o con la variable de entorno `Catalogo__Modo`
(`Local`, `Remoto` o `Soap`). **Sin recompilar**, y sin que `FachadaAplicacion` ni las páginas se
enteren de cuál quedó enchufada. En modo `Soap` las opciones de vehículo (`ICatalogoVehiculos`)
siguen por REST: el servicio SOAP publica solo la consulta de compatibilidad. Es el modo con que
arranca la Web en docker compose, así que **cada búsqueda pasa por los dos protocolos**.

### Por qué esto no es una complicación gratuita

La Segunda Parte lo puso a prueba: la vía SOAP entró como otra implementación de la misma interfaz.
`FachadaAplicacion`, los servicios de negocio y las páginas no cambiaron una línea; en la Web solo
cambió la fábrica (un caso más en el `switch`) y la configuración. El único cuidado fue que
`CatalogoSoap` traduzca los errores de WCF a `HttpRequestException`, la excepción que los
consumidores ya atrapaban para el camino REST (sección 6.8).

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
Web (líneas `Evidencia acceso ... duracion=`). Después repite la medición con la Web en modo
`Soap` contra `Catalogo.Soap` (`12-catalogo-soap.log`, `13-web-modo-soap.log` y
`14-medicion-soap.json`) y guarda el contrato SOAP: `10-wsdl.xml` y su esquema de tipos,
`11-wsdl-tipos.xsd`. Al final corre las pruebas automatizadas y deja su resultado en `08-pruebas.txt`
(sección 5.4).

### 5.2 Latencia medida

Consulta: repuestos compatibles con un **Volkswagen Gol 2015 1.6**. Los tres caminos devuelven
**los mismos 7 códigos**, en el mismo orden. Cada petición a `GET /evidencia/catalogo` crea un
ámbito nuevo y, por lo tanto, un `DbContext` nuevo. Valores de la corrida del 2026-11-10, tomados
de `01-web-modo-local.log`, `02-web-modo-remoto.log` y `13-web-modo-soap.log`:

| Ejecución | Local | Remoto (REST) | SOAP |
|---|---|---|---|
| Primera llamada (arranque en frío) | 368,5 ms | 763,1 ms | 2.252,9 ms |
| Segunda llamada (todavía calentando) | 103,8 ms | 109,6 ms | 178,7 ms |
| **En régimen: llamadas 3 a 12, mediana (rango)** | **2,8 ms** (1,3–21,6) | **11,8 ms** (5,1–27,1) | **24,6 ms** (9,8–293,9) |

`06-medicion-local.json`, `07-medicion-remota.json` y `14-medicion-soap.json` guardan solo la
última llamada de cada modo (1,3 ms, 26,0 ms y 293,9 ms). Una sola muestra puede caer en cualquier
punto del rango —en esta corrida, la última SOAP fue un pico, la más lenta de las diez—, por eso
para comparar conviene mirar la mediana.

La primera llamada incluye la compilación JIT y la caché de consultas de EF Core; en modo Remoto,
además, la apertura de la conexión HTTP, y en SOAP la construcción del canal WCF del lado del
cliente y del despachador de CoreWCF del lado del servidor. Por eso no sirve para comparar los
caminos. En régimen, la invocación directa resuelve la consulta en un par de milisegundos, el
salto por HTTP con JSON le suma unos **9 ms** y SOAP, con su sobre XML y el serializador de
contratos, unos **22 ms**. Los valores exactos cambian de una corrida a otra (esta máquina mostró
picos en los tres modos), pero el orden se mantiene. Es el precio de poder desplegar el componente
de catálogo por separado y de ofrecerle a otros sistemas un contrato formal.

### 5.3 Trazabilidad entre procesos

`CorrelacionMiddleware` asigna un `X-Correlation-Id` y `PropagacionCorrelacionHandler` lo copia a
las llamadas salientes. Una misma operación en modo Remoto queda así en los dos archivos de log:

```
# Web  (docs/evidencias/02-web-modo-remoto.log)
[21:49:54 INF] [Web] [fb6e66a242ec] Sending HTTP request GET http://localhost:5081/api/repuestos/compatibles?*
[21:49:54 INF] [Web] [fb6e66a242ec] Catalogo[Remoto] compatibilidad Volkswagen Gol 2015 (1.6) sistema=(todos) -> 7 repuestos
[21:49:54 INF] [Web] [fb6e66a242ec] Evidencia acceso origen=Remoto duracion=26,0ms vehiculo=Volkswagen Gol 2015 (1.6) resultados=7

# Catalogo.Api  (docs/evidencias/03-catalogo-api.log)  ← mismo identificador
[21:49:54 INF] [Catalogo.Api] [fb6e66a242ec] Catalogo[Local] compatibilidad Volkswagen Gol 2015 (1.6) sistema=(todos) -> 7 repuestos
[21:49:54 INF] [Catalogo.Api] [fb6e66a242ec] HTTP GET /api/repuestos/compatibles responded 200 in 22.1046 ms
```

Por SOAP pasa lo mismo: `CorrelacionSoapHandler` agrega la cabecera al pedido HTTP que lleva el
sobre SOAP, y `Catalogo.Soap` la registra con el mismo identificador:

```
# Web  (docs/evidencias/13-web-modo-soap.log)
[21:51:23 INF] [Web] [e41e77a14eb1] Catalogo[Soap] compatibilidad Volkswagen Gol 2015 (1.6) sistema=(todos) -> 7 repuestos
[21:51:23 INF] [Web] [e41e77a14eb1] Evidencia acceso origen=Soap duracion=27,5ms vehiculo=Volkswagen Gol 2015 (1.6) resultados=7

# Catalogo.Soap  (docs/evidencias/12-catalogo-soap.log)  ← mismo identificador
[21:51:23 INF] [Catalogo.Soap] [e41e77a14eb1] SOAP ConsultarPorVehiculo Volkswagen Gol 2015 1.6 sistema=(todos) -> 7
[21:51:23 INF] [Catalogo.Soap] [e41e77a14eb1] HTTP POST /ConsultaCompatibilidad.svc responded 200 in 18.8289 ms
```

Con dos procesos ya hace falta. El mismo identificador llega a `IA.Api` cuando la Web la usa en
modo remoto, y a la cola: `PublicadorEventos` lo pone en el mensaje (y en la cabecera
`X-Correlation-Id` de AMQP) y el consumidor del worker lo vuelve a poner en su contexto de log. Así
una salida a la tienda se sigue desde la Web hasta el worker con un solo id.

### 5.4 Verificación automatizada

`tests/RepMatch.Tests/Integracion/AccesoLocalVsRemotoTests.cs` es la versión ejecutable de esta
evidencia: hospeda `Catalogo.Api` en memoria con `WebApplicationFactory` y verifica que las dos
implementaciones coincidan para cinco configuraciones de vehículo, el listado completo,
códigos existentes e inexistentes y el filtro por sistema. También compara las opciones vehiculares
completas y comprueba que los modelos de la semilla tengan piezas compatibles.
`tests/RepMatch.Tests/IA/` hace lo mismo con el componente de IA: hospeda `IA.Api` en memoria, sin
clave de LLM, y compara su respuesta con la del cliente remoto; prueba el clasificador local sobre
un conjunto fijo de síntomas y el adapter del LLM con respuestas grabadas, sin red.
`tests/RepMatch.Tests/Ofertas/` prueba el adaptador VTEX con respuestas reales grabadas de Easy y
Frávega (`tests/fixtures/vtex/`) y `ServicioOfertas` con tiendas simuladas: tienda caída, tope
global de tiempo, deduplicación, vencimiento, permisos por rol y piezas recomendadas por el mecánico.
`tests/RepMatch.Tests/Mensajeria/` prueba la cola sin broker: el contrato JSON y su versión, la
regla de reintento y DLQ, qué publica `PublicadorEventos` (y que el texto libre no viaja), que con
el broker inalcanzable la publicación falla rápido y no vuelve a esperar, y la idempotencia del
registro de comisiones. Contra un RabbitMQ real lo prueban las E2E (`ColaTests`).
`tests/RepMatch.Tests/Integracion/AccesoSoapTests.cs` cubre el camino SOAP: hospeda
`Catalogo.Soap` en memoria, lo consume con el proxy generado desde el WSDL y verifica que devuelva
lo mismo que el catálogo en proceso, que el WSDL publique las cuatro operaciones y que una caída
del servicio llegue como `HttpRequestException`.

El resultado de la última corrida está en
[`docs/evidencias/08-pruebas.txt`](evidencias/08-pruebas.txt): fecha, commit, advertencias de
compilación y el resumen de `dotnet test`. Lo genera `bash scripts/evidencia-pruebas.sh`, que
también corre al final de `scripts/evidencias.sh`. El total no se copia en este informe: así,
sumar una prueba no obliga a editar la documentación, y el número entregado sale de una corrida
real. Las mismas pruebas corren sin el SDK con `docker compose --profile test run --build --rm tests`.

### 5.5 Pruebas funcionales de punta a punta

`tests/RepMatch.E2E` recorre la aplicación con un navegador real (Playwright) contra el entorno
completo de docker compose, como lo haría una persona: registro e ingreso, búsquedas con y sin
sesión, el componente de IA, los precios de las tiendas con los tres roles, la cola de mensajes, garage, perfil, la
colaboración entre cliente y mecánico, la vista de celular y los permisos (páginas sin sesión, rol equivocado, consultas ajenas). Prueba los
errores además del camino feliz, y cualquier prueba falla si la Web muestra el cartel de error
inesperado.

`bash scripts/e2e.sh` lo hace todo: arranca de una base limpia, levanta la Web principal (repuestos
por SOAP, vehículos e IA por REST) y la Web en modo Local, y corre seis fases. La primera tiene
todo arriba, así que cada búsqueda prueba también el camino SOAP. Las otras frenan `ia-api`,
`catalogo-api`, `catalogo-soap`, `tienda-simulada` y `rabbitmq`, para comprobar que la búsqueda
sigue sin IA, que la app no se cae sin catálogo REST ni SOAP, que sin tiendas se ven las piezas con
un aviso y que sin cola se puede buscar, compartir y salir a la tienda.
Corre sin clave de LLM para que el diagnóstico sea determinista, y con los precios de
`tienda-simulada`, que responde con búsquedas reales grabadas de Easy y Frávega: así las pruebas
pueden afirmar precios exactos sin depender de lo que publiquen ese día. Con `--evidencia` deja el
resumen de las seis fases en `docs/evidencias/09-pruebas-e2e.txt`.

Las pruebas que encontraron errores reales quedan omitidas con la causa explicada, hasta que se
corrijan. La lista está en [`tests/RepMatch.E2E/README.md`](../tests/RepMatch.E2E/README.md).

---

## 6. Decisiones de diseño y sus fundamentos

### 6.1 Fuentes de ofertas: APIs públicas oficiales, no scraping

Se relevaron las fuentes reales disponibles antes de comprometer el diseño:

| Fuente | Resultado | Decisión |
|---|---|---|
| MercadoLibre `sites/MLA/search` | **HTTP 403 forbidden** sin token — cerraron la búsqueda pública | ❌ Descartada |
| `argautopartes.com.ar` (Tiendanube) | `robots.txt` con `Disallow: /search/` | ❌ Descartada |
| **VTEX `easy.com.ar`** | HTTP 206 con autopartes reales y precio, endpoint público, permitido por `robots.txt` | ✅ Implementada |
| **VTEX `fravega.com`** | HTTP 206, mismo contrato | ✅ Implementada |
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

### 6.5 RabbitMQ.Client directo, no MassTransit

MassTransit 9 pasó a licencia comercial y la rama 8 (Apache-2.0) queda sin soporte a fin de 2026.
Más allá de la licencia: la consigna evalúa *"implementación de productores y consumidores de
mensajes"*, y escribirlos explícitamente sobre `RabbitMQ.Client` 7.2.2 muestra la mecánica en vez de
esconderla detrás de un framework. Está envuelto en una interfaz propia, `IEventBus`, en
`Contracts` (sección 6.10).

### 6.6 Versiones y seguridad

La plantilla de `webapi` arrastraba `Microsoft.OpenApi` 2.0.0, afectado por **CVE-2026-49451**
(denegación de servicio por recursión no controlada al parsear documentos OpenAPI con referencias
circulares, CVSS 7.5). Se fijó la versión **2.12.2**, posterior al parche 2.7.5, y se alineó
`Microsoft.AspNetCore.OpenApi` en 10.0.11. La solución compila **sin advertencias**.

### 6.7 IA: el modelo elige entre piezas reales, con respaldo local

La consigna permite "un modelo de NLP o API de LLM" y que el componente se exponga por REST o por
la cola. Se tomaron cuatro decisiones:

- **El LLM no inventa códigos.** El catálogo resuelve primero qué piezas le entran al vehículo, y
  esa lista viaja en la petición (`SolicitudDiagnosticoDto.Candidatos`). El modelo elige entre ellas
  y `DiagnosticadorLlm` descarta cualquier código que no esté en la lista. Así la compatibilidad la
  sigue garantizando el catálogo, y `IA.Api` no necesita base de datos.
- **Siempre hay respuesta.** Sin clave, con error de red, con timeout (`IA:Llm:TimeoutSegundos`,
  4 s por defecto) o con una respuesta fuera del esquema, `DiagnosticadorConRespaldo` conmuta al
  clasificador local (TF-IDF y vecinos más cercanos sobre `Datos/sintomas.json`) y lo registra en el
  log. La respuesta indica la fuente (`llm` o `fallback-local`) y la latencia: son las métricas que
  va a pedir el Integrador. Sin LLM, el clasificador es además la línea base contra la que medirlo.
- **Proveedor intercambiable.** Groq y Gemini ofrecen la API de chat completions compatible con
  OpenAI con cupo gratuito; cambiar de proveedor es cambiar `IA:Llm:UrlBase` y `IA:Llm:Modelo`. La
  clave llega solo por variable de entorno o por el `.env`, que no se commitea.
- **Privacidad.** El texto sale hacia el proveedor sin mails ni teléfonos
  (`FiltroDatosPersonales`), y los logs registran el largo del texto, no su contenido. En la
  búsqueda sin sesión el texto se diagnostica pero no se guarda.

### 6.8 SOAP: un contrato formal sobre el mismo catálogo

La consigna pide al menos un servicio SOAP con WSDL y contrato. Las decisiones:

- **CoreWCF en un host propio.** En .NET moderno WCF existe solo como cliente; CoreWCF es su
  versión de servidor sobre ASP.NET Core. `Catalogo.Soap` va aparte de `Catalogo.Api` para que cada
  host tenga un protocolo, su propio puerto (8082) y su propio healthcheck. Por dentro usa el mismo
  `CatalogoLocal` que el host REST, así los dos caminos devuelven exactamente lo mismo.
- **Contrato propio, no los DTO internos.** `IConsultaCompatibilidad` y los tipos `Repuesto` y
  `Aplicacion` del WSDL viven en el host SOAP, con espacio de nombres `urn:repmatch:catalogo:v1`. El
  WSDL es un contrato público: tiene que quedar estable aunque cambien los DTO de `Contracts`. Sus
  operaciones cubren `ICatalogoRepuestos` completo (`ConsultarPorVehiculo`, `ObtenerPorCodigo`,
  `Listar`) y suman `VerificarCodigo`, la consulta puntual que haría otro sistema que se integra.
- **El cliente sale del WSDL.** `RepMatch.Clientes.Soap` no referencia al host: su proxy lo genera
  `dotnet-svcutil` a partir del WSDL publicado (`ServicioSoap/`), que es como lo consumiría
  cualquier otro sistema. Se regenera con `dotnet-svcutil -u ServicioSoap` con el servicio levantado.
- **Errores traducidos en el adapter.** WCF informa las caídas con `EndpointNotFoundException`,
  `TimeoutException` o faults SOAP. `CatalogoSoap` las convierte en `HttpRequestException`, la que la
  fachada y el observador ya atrapaban para el catálogo remoto. Sin eso, una caída del servicio SOAP
  tiraría la página en vez de mostrar "no se pudo consultar el catálogo".
- **Que se use, no que solo exista.** En docker compose la Web busca los repuestos por SOAP y las
  opciones de vehículo y el diagnóstico por REST. Cada búsqueda de la demo usa los dos protocolos,
  y el correlation id permite seguirla en los logs de los tres procesos (sección 5.3).

### 6.9 Precios de las tiendas: lo que mostró la API real

El adaptador se diseñó a partir de respuestas reales de Easy y Frávega, grabadas en
`tests/fixtures/vtex/`:

- **Las tiendas generalistas tienen pocos repuestos.** Del catálogo semilla, Easy publica baterías
  de auto y casi nada más; Frávega vende sobre todo accesorios. La pantalla lo dice ("Ninguna tienda
  conectada la publica por ahora") en vez de mostrar una lista vacía. eBay (#17) es la fuente
  pensada para dar profundidad.
- **Buscar por nombre trae basura.** "Batería" devuelve taladros y pilas; "pastillas de freno", las
  pastillas para encender el fuego. Cada tienda configura sus categorías válidas
  (`PrefijosCategoria`: `/Automotor/` en Easy) y, en la búsqueda por nombre, el título tiene que
  empezar con la misma palabra que la pieza. La aplicación descarta coincidencias probables que
  contradicen los datos disponibles del vehículo: modelo, marca, año, posición o sistema. Si la
  publicación no trae esos datos, no se presume incompatibilidad; queda como probable y se advierte
  ("verificá que sea la medida de tu auto"). En la búsqueda por código equivalente, el código debe
  aparecer en la publicación.
- **Frávega es un marketplace.** Cada producto trae varios vendedores y el de la propia tienda suele
  estar sin stock: se toma el vendedor disponible más barato y se muestra quién vende.
- **La búsqueda no informa el costo de envío.** Obtenerlo requiere simular la compra con un código
  postal. El precio se muestra como de referencia y la pantalla aclara que el envío se calcula en la
  tienda; `Oferta.PrecioTotal` ya lo suma cuando una fuente lo informe.
- **Identidad por EAN.** Si distintas tiendas publican el mismo EAN, sus ofertas se clasifican como
  coincidencias exactas de la misma pieza; se mantienen separadas porque son publicaciones y precios
  distintos.
- **Uso responsable y resiliente de las APIs.** Cada fuente limita pedidos simultáneos
  (`PedidosSimultaneos`), cachea respuestas por término (`CacheMinutos`) y tiene timeout, reintentos
  y circuito independiente. En Compose la caché es Redis compartida; sin `ConnectionStrings:Redis`
  se usa caché distribuida en memoria. Además, `ServicioOfertas` aplica un tope global
  (`TimeoutGlobalSegundos`): si una tienda falla, se muestran las demás y no se marcan como agotadas
  las ofertas guardadas de la tienda que no respondió. Los fallos de caché se registran y la consulta
  continúa por red.

Cada rol ve los precios de una forma distinta. El **cliente** ve las ofertas de cada pieza y sale a
la tienda por `/derivar`, que registra la comisión. El **mecánico** ve las mismas ofertas con enlace
directo a la tienda: si pasara por `/derivar`, su propio clic contaría como una derivación atribuida
a él. Si corrige el diagnóstico, también se buscan los precios de las piezas que recomendó, para que
el cliente pueda comprarlas. **Sin sesión** se ve "desde $X en N tiendas" con un "Ingresá para ver
dónde".

### 6.10 Cola de mensajes: qué se publica y para qué

La consigna pide *"mecanismos de mensajería y colas"* y *"productores y consumidores de mensajes"*.
Los mensajes de RepMatch son **eventos de integración**: hechos que ya ocurrieron y quedaron
guardados (`busqueda.creada`, `busqueda.compartida`, `diagnostico.validado`, `derivacion.tienda`).
Sirven para lo que tiene que pasar como consecuencia de una acción, que la persona no necesita
esperar y que no se puede perder si quien lo hace está caído. El primer caso real es la comisión:
"Ir a la tienda" redirige al instante y el registro de la derivación por tienda y mes lo hace el
worker después, desde la cola. La topología, las garantías y cómo seguir un mensaje en la demo
están en el [diagrama de flujo de mensajes](diagramas/mensajes.md).

Las decisiones:

- **Despacho compuesto, búsqueda sincrónica.** La cola no reemplaza al observador en proceso:
  `ObservadorCompatibilidad` sigue completando la búsqueda en la misma petición y
  `PublicadorEventos` publica después. Mover la resolución de piezas a la cola obligaría a la
  pantalla a esperar o consultar a un worker.
- **Un contrato propio y versionado.** Todos los mensajes viajan en `MensajeIntegracion<T>` (id del
  mensaje, tipo, versión, fecha, correlation id y datos) en JSON. Un campo nuevo no rompe a un
  consumidor viejo; cambiar el significado de uno obliga a subir la versión. Los datos llevan ids y
  lo mínimo para actuar, **nunca el texto libre** ni datos personales: un mensaje puede quedar en
  la cola, en la DLQ o en un log.
- **Topología compartida y protegida contra arranques simultáneos.** Exchange topic
  `repmatch.eventos`, una cola durable por consumidor y un exchange de mensajes muertos con una DLQ
  por cola. `TopologiaRabbitMq` es la fuente única de nombres y claves: los publicadores declaran
  todas las colas conocidas al conectarse y cada consumidor vuelve a declarar la propia. Las
  declaraciones son idempotentes, así que el primer evento no depende de que el worker haya
  arrancado antes. La publicación usa `mandatory`; si RabbitMQ devuelve un mensaje sin ruta, queda
  registrado como error en el log en vez de parecer entregado.
- **El broker no es requisito para usar la app.** Si RabbitMQ no responde, la publicación falla, se
  registra y la operación de la persona sigue: el hecho ya está en la base. Después de una falla
  no se intenta conectar por 30 segundos, para que un broker caído no le sume el timeout a cada
  clic. El costo es que ese mensaje se pierde; garantizarlo necesitaría un outbox, que quedó fuera
  de alcance. Sin `Mensajeria:Habilitada` (fuera de Docker) no se publica nada.
- **Consumir sin perder ni duplicar.** El consumidor confirma el mensaje recién después de
  procesarlo; si falla, lo reintenta una vez y después lo manda a la DLQ; si no se puede leer, va
  directo. Como un mensaje puede llegar dos veces, el registro de comisiones guarda el id del
  mensaje procesado en la misma transacción que la suma.
- **Un worker aparte.** `RepMatch.Notificaciones.Worker` corre en su propio contenedor: si se cae,
  los mensajes esperan en la cola y se procesan cuando vuelve, sin que la Web se entere. Las
  notificaciones a cliente y mecánico, y la búsqueda de precios en segundo plano, son los próximos
  consumidores (sección 7).


---

## 7. Cómo esta arquitectura recibe las entregas siguientes

| Entrega | Qué agrega | Dónde se enchufa | Qué NO hay que tocar |
|---|---|---|---|
| **Primera** | Patrones formalizados, fachada, eventos de dominio internos | `DespachadorEventos` publica `BusquedaCreada` a `ObservadorCompatibilidad`. Las operaciones pasan por `FachadaAplicacion`; `SelectorVehiculo` consulta `ICatalogoVehiculos` directamente | Dominio, contratos |
| **Segunda** *(implementado)* | SOAP `ConsultaCompatibilidad` (CoreWCF + WSDL) | `Catalogo.Soap` publica el servicio; `CatalogoSoap` es la tercera implementación de `ICatalogoRepuestos`, elegida con `Catalogo:Modo=Soap` (sección 6.8) | Presentación, lógica de negocio, interfaz del catálogo |
| **Segunda** | REST documentados con OpenAPI | `Catalogo.Api` e `IA.Api` publican `/openapi/v1.json` | — |
| **Segunda** *(implementado)* | RabbitMQ, productores y consumidores | `PublicadorEventos` es un observador más de los eventos de dominio y publica por `IEventBus` (`BusRabbitMq`); el worker consume `derivacion.tienda` (registro de comisiones) y todos los eventos para el log de actividad (sección 6.10) | `ServicioBusquedas`, `DespachadorEventos`, entidades |
| **Segunda** | Notificaciones y precios en segundo plano | Consumidores nuevos en el worker: una cola atada a `busqueda.compartida` o `diagnostico.validado` para avisar al mecánico o al cliente, y otra a `busqueda.creada` que llame a `ServicioOfertas.ObtenerAsync`, el mismo método que hoy dispara la página | Productores, topología, `ConsumidorRabbitMq<T>`, la pantalla de precios |
| **Segunda** *(implementado)* | Servicio de Ofertas y API externa (VTEX: Easy y Frávega) | `IFuenteOfertas` en `Contracts`; `FuenteVtex` en `RepMatch.Fuentes.Vtex`, con caché distribuida y resiliencia por tienda; `ServicioOfertas` agrega resultados, clasifica EAN y valida coincidencias por nombre (sección 6.9) | `ServicioBusquedas.CrearAsync`, catálogo |
| **Segunda** | Adaptador a eBay | Otra implementación de `IFuenteOfertas`, en USD; el ranking no mezcla monedas | `ServicioOfertas`, presentación |
| **Segunda** *(implementado)* | Componente de IA (texto libre → repuestos) | `ObservadorCompatibilidad` usa `ServicioDiagnostico`, que le pasa `Busqueda.TextoLibre` y las piezas compatibles a `IDiagnosticador` (local o remota contra `IA.Api`, por `IA:Modo`) | `ServicioBusquedas.CrearAsync`; en la presentación solo se agregó la tarjeta del diagnóstico |
| **Integrador** | Métricas de IA, pruebas de carga, despliegue | `DiagnosticoValidado` trae, por cada revisión, los códigos propuestos y los que recomendó el mecánico: el dataset etiquetado para medir exactitud | — |
| **Integrador** | Pruebas funcionales | Ya existe `tests/RepMatch.E2E` (sección 5.5): cada pantalla nueva suma sus pruebas ahí y `scripts/e2e.sh` las corre contra el entorno completo | — |

El punto de extensión quedó en el evento: `ServicioBusquedas.CrearAsync` persiste la búsqueda y
despacha `BusquedaCreada` igual que en la Primera Parte. La IA se sumó del lado del observador, que
ahora llena `CodigosObjetivo` con las piezas compatibles que eligió el diagnóstico.

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
dotnet run --project src/RepMatch.Catalogo.Soap --urls http://localhost:5082
```

```bash
dotnet run --project src/RepMatch.IA.Api --urls http://localhost:5083
```

```bash
dotnet run --project src/RepMatch.Web --urls http://localhost:5080
```

La Web sola alcanza: por defecto resuelve catálogo e IA en proceso y no publica en la cola
(`Mensajeria:Habilitada=false`). Los precios salen de las
tiendas reales, así que necesitan internet; sin conexión la búsqueda funciona igual y avisa que no
pudo consultar las tiendas. Para trabajar sin red está el simulador:
`dotnet run --project tests/RepMatch.TiendaSimulada --urls http://localhost:5084`, con la Web
arrancada con `Ofertas__Tiendas__0__UrlBase=http://localhost:5084/easy` y
`Ofertas__Tiendas__1__UrlBase=http://localhost:5084/fravega`. Para el modo SOAP, con
`Catalogo.Api` y `Catalogo.Soap` levantados, se arranca la Web con `Catalogo__Modo=Soap`. La
evidencia de los tres caminos se genera con `bash scripts/evidencias.sh`.

### Con Docker

```bash
docker compose up --build
```

Web en `http://localhost:8080`, Catálogo REST en `http://localhost:8081/salud`, Catálogo SOAP en
`http://localhost:8082/ConsultaCompatibilidad.svc?wsdl`, IA en `http://localhost:8083/salud`,
RabbitMQ en `5672` con su UI en `http://localhost:15672` (usuario y contraseña `repmatch`), el
worker en `http://localhost:8085/salud`, PostgreSQL en `5432`. En compose la Web arranca en modo **Soap**: repuestos por SOAP, opciones de
vehículo e IA por REST. El acceso remoto entre componentes, y los dos protocolos de integración,
quedan demostrados apenas se levanta el entorno.

---

## 9. Entregables del TP Inicial

| Requisito de la consigna | Dónde está |
|---|---|
| Entorno configurado (.NET 10, contenedores) | `docker-compose.yml`, `src/*/Dockerfile` |
| ≥3 componentes reutilizables independientes | 6 componentes en esa entrega, 10 hoy — sección 2 |
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
| Servicios de negocio e integración por eventos | `ServicioClientes`, `ServicioBusquedas`, `ServicioMecanicos`, `ServicioRevisiones`, `ServicioDerivaciones`, `ServicioOfertas`, `ObservadorCompatibilidad` |
| Servicio de Inventario | `ServicioOfertas`: como RepMatch no almacena stock, su inventario son las ofertas de las tiendas, consultadas en cada búsqueda, sin duplicar y con vencimiento. Justificación en la nota "Sobre el Servicio de Inventario" de la sección 2. |
| Diagrama de arquitectura en capas | [`docs/diagramas/capas.md`](diagramas/capas.md) |
| Diagramas de secuencia (búsquedas, login, alta de vehículo, catálogo, eliminación de cuenta y diagnóstico por IA) | [`docs/diagramas/secuencia.md`](diagramas/secuencia.md) |
