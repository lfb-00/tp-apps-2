# CLAUDE.md

Guía para trabajar en este repo. Leela entera antes de tocar código o docs.

## Qué es

**RepMatch**: meta-buscador de repuestos automotrices con diagnóstico asistido por IA. Es el trabajo
práctico de **Desarrollo de Aplicaciones II (UADE, Mg. Christian Parkinson)**, que se entrega en
cuatro etapas sobre el mismo código. Repo: `lfb-00/tp-apps-2`. El trabajo se organiza con issues de
GitHub (labels `tp-primera`, `patrones`, `P0-urgente`, …) que traen criterios de aceptación
alineados con la consigna.

RepMatch **compara** ofertas de tiendas reales y deriva al vendedor: **no vende, no cobra, no guarda
stock**. Es una decisión deliberada (el docente descartó el e-shop clásico) y no hay que romperla:
nada de carrito, pagos propios ni inventario.

Mapeo con los nombres de la consigna: `Cliente` = Cliente · `Repuesto` + `Oferta` = Producto ·
`Busqueda` = Pedido. Cuando la consigna diga "PedidoCreado" o "Servicio de Pedidos", acá es
`BusquedaCreada` / `ServicioBusquedas`.

## La consigna, por etapa

Los docs son entregables evaluados tanto como el código. Cada etapa suma sobre la anterior.

### TP Inicial — arquitectura de componentes (**entregado**)
- Entorno configurado (.NET + contenedores Docker).
- ≥3 componentes reutilizables independientes: dominio, acceso a datos (Repository), utilidad
  (validación, logging, configuración).
- Aplicación multicapa presentación → negocio → datos que los use.
- Gestión de dependencias (NuGet).
- Acceso local vs. remoto entre componentes (invocación directa vs. interfaz/remoting).
- **Entregable:** diagramas de clases, componentes y despliegue + código + informe breve de
  arquitectura + evidencias de ejecución local y remota.

### Primera Parte — arquitectura de aplicaciones (**implementada**)
- Framework empresarial (.NET Core / ASP.NET Core ✔).
- Patrones: **Factory, Repository, Strategy, Observer, Facade**.
- Servicios orientados a componentes (en la consigna: Pedidos, Inventario, Clientes).
- Integración entre módulos internos mediante **interfaces y eventos de dominio**.
- Modelado de procesos (BPMN simplificado o diagramas de secuencia) y **arquitectura en capas con
  capa de servicios**.
- **Entregable:** app funcional + documentación de patrones aplicados + diagrama de capas.

### Segunda Parte — integración sincrónica y asincrónica (**en curso**: IA, SOAP y API externa VTEX implementados)
- ≥1 servicio **SOAP** (WSDL + contrato) y ≥2 servicios **REST** (OpenAPI/Swagger).
- Mensajería con colas (RabbitMQ decidido), productores y consumidores (ej. `BusquedaCreada`).
- Integración SOA / microservicios ligeros.
- ≥1 **API externa real** (VTEX implementado: Easy y Frávega; eBay pendiente. APIs oficiales, nunca scraping).
- **Componente de IA** (acá: texto libre del problema → diagnóstico y repuestos
  candidatos), expuesto por REST o invocado asincrónicamente por la cola.
- **Entregable:** servicios documentados + productores/consumidores + API externa + IA funcional +
  evidencias de prueba.

### Integrador — solución distribuida completa
- Requerimientos funcionales y no funcionales (incluye IA: latencia, exactitud mínima, privacidad).
- Diagramas de componentes, secuencia, despliegue y flujo de mensajes.
- Comunicación sincrónica (REST/SOAP) y asincrónica (colas + eventos).
- Pruebas funcionales, de integración, de carga básica y validación de la IA.
- Documentación técnica: arquitectura, contratos, configuración de colas, decisiones de IA, manual
  de despliegue. Defensa con demo en vivo.
- **Mínimos de la solución final:** multicapa/componentes/SOA, 1 SOAP + varios REST, cola con
  productores y consumidores, IA integrada y consumible, **manejo de errores, logging y
  configuración externalizada**, ejecutable en laboratorio (contenedores).

## Estructura de la solución

.NET 10 · ASP.NET Core · Blazor Server · EF Core 10 (Npgsql / InMemory) · PostgreSQL 17 · Serilog ·
FluentValidation · xUnit. Solución: `RepMatch.slnx`.

```
src/
  RepMatch.Domain/         Dominio: entidades, value objects (Dinero, DatosVehiculo), eventos,
                           interfaces I*Repository e IUnitOfWork. SIN dependencias.
  RepMatch.Common/         Utilidad: Serilog + CorrelacionMiddleware, validación, opciones,
                           ResultadoOperacion<T>, ResultadoMedido/Cronometro. SIN refs internas.
  RepMatch.Contracts/      DTOs, ICatalogoRepuestos, ICatalogoVehiculos, IDiagnosticador, IFuenteOfertas,
                           OpcionesCatalogo, OpcionesIA, OpcionesOfertas. SIN refs internas.
  RepMatch.Persistence/    Acceso a datos: RepMatchDbContext, Configuraciones/ (todo el mapeo EF),
                           repositorios, UnitOfWork, DatosSemilla. → Domain, Common
  RepMatch.Aplicacion/     Lógica de negocio: FachadaAplicacion, ServicioClientes, ServicioBusquedas,
                           ServicioMecanicos, ServicioRevisiones, ServicioDerivaciones,
                           ServicioDiagnostico, ServicioOfertas, DespachadorEventos, ObservadorCompatibilidad,
                           validadores, mapeadores,
                           CatalogoLocal, CatalogoVehiculosLocal. → Domain, Common, Contracts
  RepMatch.Clientes.Rest/  Adaptadores remotos: CatalogoRemoto, CatalogoVehiculosRemoto y
                           DiagnosticadorRemoto (HttpClient tipado). → Common, Contracts
  RepMatch.Clientes.Soap/  Adaptador SOAP: CatalogoSoap (tercera ICatalogoRepuestos) sobre el proxy que
                           genera dotnet-svcutil desde el WSDL (ServicioSoap/, no editar a mano).
                           → Common, Contracts. No referencia al host SOAP.
  RepMatch.IA/             Componente de IA: DiagnosticadorLlm (Groq/Gemini, API compatible con
                           OpenAI), DiagnosticadorLocal (TF-IDF + k-NN sobre Datos/sintomas.json,
                           embebido), DiagnosticadorConRespaldo, FiltroDatosPersonales. → Common, Contracts
  RepMatch.Fuentes.Vtex/   API externa: FuenteVtex (un IFuenteOfertas por tienda de Ofertas:Tiendas) y
                           LectorVtex (JSON del buscador de VTEX → OfertaExternaDto). → Common, Contracts
  RepMatch.Catalogo.Api/   Host REST del catálogo (controllers, OpenAPI en /openapi/v1.json).
                           Siempre resuelve ICatalogoRepuestos con CatalogoLocal e
                           ICatalogoVehiculos con CatalogoVehiculosLocal; IDiagnosticador en proceso.
  RepMatch.Catalogo.Soap/  Host SOAP del catálogo (CoreWCF): contrato IConsultaCompatibilidad, WSDL
                           en /ConsultaCompatibilidad.svc?wsdl. Resuelve con CatalogoLocal.
  RepMatch.IA.Api/         Host REST de la IA: POST /api/diagnostico, OpenAPI. Sin base de datos.
  RepMatch.Web/            Presentación Blazor Server. FabricaCatalogo elige Local/Remoto/Soap y
                           FabricaDiagnostico, Local/Remoto.
tests/RepMatch.Tests/      Dominio/, Persistencia/, Aplicacion/, Presentacion/, Integracion/
                           (WebApplicationFactory sobre Catalogo.Api y Catalogo.Soap) e IA/ (también sobre IA.Api)
tests/RepMatch.E2E/        Pruebas de punta a punta con Playwright contra la app levantada. NO está
                           en RepMatch.slnx a propósito (el `dotnet test` de la raíz corre sin
                           entorno); se corren con scripts/e2e.sh. Ver su README.
tests/RepMatch.TiendaSimulada/  Simulador de tiendas VTEX para los E2E (respuestas reales grabadas en
                           tests/fixtures/vtex/). Está en RepMatch.slnx: solo compila, no tiene pruebas.
docs/                      informe-arquitectura.md, patrones.md, diagramas/*.md (Mermaid), evidencias/
scripts/evidencias.sh      regenera docs/evidencias/ (logs + mediciones local vs. remoto)
```

### Ideas centrales que no hay que romper
- **Dirección de dependencias**: `Domain`, `Common` y `Contracts` no referencian ningún otro
  proyecto de la solución; es el argumento de "componentes independientes". `Domain` no conoce EF
  Core ni ASP.NET: el mapeo vive en `Persistence/Configuraciones/` (incluido `Ignore(EventosDominio)`).
- **Acceso local vs. remoto**: `ICatalogoRepuestos` (Strategy) con `CatalogoLocal` y
  `CatalogoRemoto` (Adapter); `FabricaCatalogo.AgregarCatalogo` (Factory) decide por
  `Catalogo:Modo` al arrancar y registra **ambas** concretas, pero `ICatalogoRepuestos` queda enlazada
  a una sola. `GET /evidencia/catalogo` mide solo la activa; `scripts/evidencias.sh` compara los dos
  caminos levantando la Web dos veces, una con cada valor de `Catalogo:Modo` (la página `/acceso`
  ya no existe).
  `ExtensionesAplicacion` **no** registra `ICatalogoRepuestos` a propósito: es decisión del host.
  `CatalogoSoap` es la tercera estrategia (Segunda Parte), sin tocar interfaz ni consumidores. En
  `Catalogo:Modo=Soap` los repuestos van por SOAP y los vehículos por REST; es el modo de la Web en
  compose, para que cada búsqueda use los dos protocolos. `CatalogoSoap` traduce los errores de WCF
  a `HttpRequestException`: es lo que atrapan la fachada y el observador, no lo cambies.
- **Errores esperables → `ResultadoOperacion<T>`**, no excepciones. Excepciones solo para lo
  verdaderamente excepcional.
- **Eventos de dominio**: `EntidadBase` acumula eventos (`RegistrarEvento`); `Busqueda` emite
  `BusquedaCreada`. El despacho in-process (`DespachadorEventos` → `ObservadorCompatibilidad`,
  Observer) es de la Primera Parte. En la Segunda se registrará otra implementación de
  `IDespachadorEventos` que publique a través de un `IEventBus` propio (sobre `RabbitMQ.Client`
  directo, no MassTransit), sin tocar `ServicioBusquedas`.
- **Catálogo vehicular**: `ICatalogoVehiculos` tiene estrategias local y remota elegidas por
  la misma fábrica. `SelectorVehiculo` lo consulta directamente. La fachada valida la configuración
  antes de buscar piezas.
- **IA**: `ObservadorCompatibilidad`, al recibir `BusquedaCreada`, delega en `ServicioDiagnostico`:
  el catálogo da las piezas compatibles (filtro duro) y `IDiagnosticador` elige entre ellas leyendo
  `Busqueda.TextoLibre`. El LLM nunca inventa códigos: recibe los candidatos y lo que no está en la
  lista se descarta. Sin clave, con error o timeout responde el clasificador local
  (`Fuente = "fallback-local"`); si la IA remota no responde, quedan todas las compatibles.
  `ServicioBusquedas.CrearAsync` no cambió. `Busqueda.RegistrarDiagnostico` guarda sistema,
  urgencia, causas y fuente. La búsqueda sin sesión usa `FachadaAplicacion.DiagnosticarAsync` y no
  guarda el texto. `FabricaDiagnostico` elige por `IA:Modo`, igual que el catálogo.
- **Ofertas (el "Inventario")**: `ServicioOfertas` consulta todas las `IFuenteOfertas` en paralelo
  con un tope global y guarda lo capturado en la `Busqueda` (`ActualizarOfertas`: misma publicación
  → se actualiza y conserva el `Id`, que puede tener derivaciones; lo que una tienda que respondió
  dejó de publicar → no disponible; una tienda caída no toca sus ofertas). La página pide los precios
  **después** de mostrar las piezas: es el comportamiento que tendrá con la cola. El cliente deriva
  por `/derivar`; el mecánico ve enlaces directos (su clic no se le atribuye); sin sesión solo
  "desde $X en N tiendas". La identidad entre tiendas por EAN es el issue #29, aparte.
- **Roles y modelo de negocio**: `Cliente.Rol` distingue cliente y mecánico (misma entidad, a
  propósito). El cliente comparte una `Busqueda` con un mecánico, que confirma o corrige el
  diagnóstico (`CodigosRecomendados` queda aparte de `CodigosObjetivo`: es el dato etiquetado de la
  IA). El ingreso es comisión de afiliado por `Derivacion` (`GET /derivar/{ofertaId}`) y el plan Pro
  del mecánico, sin cobro real. El ranking de ofertas depende solo del precio: no lo atar a quién paga.
- **Configuración externalizada**: todo se sobreescribe por variable de entorno (`Seccion__Clave`):
  `Catalogo:Modo` (`Local`/`Remoto`/`Soap`), `Catalogo:UrlBaseRemota`, `Catalogo:UrlSoap`, `IA:Modo`, `IA:UrlBaseRemota`,
  `IA:Llm:*` (`ApiKey` solo por entorno o `.env`, nunca en el repo), `Persistencia:Proveedor`
  (`InMemory`/`Postgres`), `ConnectionStrings:RepMatch`, `Ofertas:*` (tiendas, timeouts, vigencia,
  cache). Puertos del host en `.env`.
- **Logging/trazabilidad**: cada host llama `UsarSerilog(nombre)` y `UsarCorrelacion()`; el id de
  correlación viaja Web → Catalogo.Api y Web → IA.Api (es parte de las evidencias).
- Base: `EnsureCreatedAsync` + `DatosSemilla`, idempotente, con reintentos. **No hay migraciones.**

### Patrones ya documentados
`docs/patrones.md` cubre Factory, Strategy, Repository, Unit of Work, Adapter, Observer y Facade,
cada uno con *dónde está / qué problema resuelve / qué pasaría sin él / código*. El diagrama de
capas está en `docs/diagramas/capas.md`. Al agregar un patrón, documentarlo con la misma estructura.

## Comandos

```bash
dotnet build && dotnet test                                            # sin Docker, InMemory
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
dotnet run --project src/RepMatch.Catalogo.Soap --urls http://localhost:5082
dotnet run --project src/RepMatch.IA.Api --urls http://localhost:5083
dotnet run --project src/RepMatch.Web --urls http://localhost:5080    # GET /evidencia/catalogo mide el modo activo
docker compose up --build --wait                                       # web 8080, api 8081, soap 8082, ia 8083, pg 5432 (Web en modo Soap)
docker compose --profile test run --rm tests                           # pruebas sin SDK
docker compose --profile local up --build web-local                    # Web en modo Local (8090)
bash scripts/evidencias.sh                                             # regenera docs/evidencias/
bash scripts/e2e.sh                                                    # Playwright: levanta todo, 3 fases, apaga (borra la base)
```

Si un cambio toca una pantalla, correr `bash scripts/e2e.sh`. Las pruebas que encontraron un
error real están con `Skip` y un comentario: al arreglar el error, sacar el `Skip`.

La solución compila **sin advertencias**; mantenerlo así.

## Convenciones

- **Todo en español**: identificadores, comentarios, docs y mensajes de commit. En código, sin
  tildes ni ñ (`Busqueda`, `logica`, `duenio`); en Markdown, con tildes.
- Comentarios XML `/// <summary>` que explican **por qué** (el docente los lee): qué pide la
  consigna, qué alternativa se descartó, qué etapa futura se enchufa ahí.
- Registro de DI por componente con métodos de extensión `Agregar*` / `Usar*`
  (`AgregarPersistencia`, `AgregarAplicacion`, `AgregarCatalogo`, `AgregarDiagnostico`,
  `AgregarDiagnosticoIA`, `UsarSerilog`, `UsarCorrelacion`).
- Endpoints de salud en `/salud` en cada host (los usan los healthchecks de Docker).
- Fuentes de ofertas: solo APIs públicas oficiales, nunca scraping.

## Al cambiar cosas, actualizar también

- **Docs**: `docs/informe-arquitectura.md` (incluye la tabla de la sección 7 "cómo recibe las
  entregas siguientes" y la de entregables), `docs/diagramas/*.md`, `docs/patrones.md` y
  `README.md`. Los diagramas se editan en los bloques Mermaid de los Markdown, que son su única fuente. Verificar su renderizado con un preview compatible.
- **Cantidad de pruebas**: no se escribe a mano en ningún doc (issue #36). La sección 5.4 del
  informe enlaza `docs/evidencias/08-pruebas.txt`, que genera `bash scripts/evidencia-pruebas.sh`;
  la 5.5 enlaza `docs/evidencias/09-pruebas-e2e.txt`, que genera `bash scripts/e2e.sh --evidencia`.
  Se regeneran al cerrar una entrega, no con cada prueba. `docker-compose.yml`, los Dockerfile y
  los README dicen "las pruebas" sin número. `status.md` sí registra el resultado de cada corrida
  con su fecha: es historial, no un total vigente.
- **Proyecto nuevo**: sumarlo a `RepMatch.slnx` **y** a los `COPY` de `.csproj` de cada
  `Dockerfile` que lo necesite (`src/*/Dockerfile`, `tests/RepMatch.Tests/Dockerfile`); si no, el
  `dotnet restore` del build en Docker falla.
- Finales de línea: `.gitattributes` fuerza LF en `*.sh`, `Dockerfile` y `*.yml` (corren en Linux).

## Trabajo en paralelo

Puede haber varias sesiones de Claude trabajando issues distintos sobre este mismo working tree.
No cambiar de rama ni crear ramas en el worktree compartido, hacer ediciones puntuales en los docs
compartidos (no reescribirlos enteros), releerlos antes de editar, y no commitear salvo pedido
explícito — y en ese caso, solo los archivos propios.
