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

### Segunda Parte — integración sincrónica y asincrónica
- ≥1 servicio **SOAP** (WSDL + contrato) y ≥2 servicios **REST** (OpenAPI/Swagger).
- Mensajería con colas (RabbitMQ decidido), productores y consumidores (ej. `BusquedaCreada`).
- Integración SOA / microservicios ligeros.
- ≥1 **API externa real** (planeado: eBay y VTEX vía APIs oficiales, nunca scraping).
- **Componente de IA** (acá: texto libre del problema → diagnóstico, urgencia y repuestos
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
  RepMatch.Contracts/      DTOs, ICatalogoRepuestos, OpcionesCatalogo. SIN refs internas.
  RepMatch.Persistence/    Acceso a datos: RepMatchDbContext, Configuraciones/ (todo el mapeo EF),
                           repositorios, UnitOfWork, DatosSemilla. → Domain, Common
  RepMatch.Aplicacion/     Lógica de negocio: FachadaAplicacion, ServicioClientes, ServicioBusquedas,
                           DespachadorEventos, ObservadorCompatibilidad, validadores, mapeadores,
                           CatalogoLocal. → Domain, Common, Contracts
  RepMatch.Clientes.Rest/  Adaptador remoto: CatalogoRemoto (HttpClient tipado + correlación).
                           → Common, Contracts
  RepMatch.Catalogo.Api/   Host REST del catálogo (controllers, OpenAPI en /openapi/v1.json).
                           Siempre resuelve ICatalogoRepuestos con CatalogoLocal.
  RepMatch.Web/            Presentación Blazor Server. FabricaCatalogo elige Local/Remoto.
tests/RepMatch.Tests/      Dominio/, Persistencia/, Integracion/ (WebApplicationFactory sobre Catalogo.Api)
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
  La futura implementación SOAP es una tercera estrategia, sin tocar interfaz ni consumidores.
- **Errores esperables → `ResultadoOperacion<T>`**, no excepciones. Excepciones solo para lo
  verdaderamente excepcional.
- **Eventos de dominio**: `EntidadBase` acumula eventos (`RegistrarEvento`); `Busqueda` emite
  `BusquedaCreada`. El despacho in-process (`DespachadorEventos` → `ObservadorCompatibilidad`,
  Observer) es de la Primera Parte. En la Segunda se registrará otra implementación de
  `IDespachadorEventos` que publique a través de un `IEventBus` propio (sobre `RabbitMQ.Client`
  directo, no MassTransit), sin tocar `ServicioBusquedas`.
- **Punto de extensión de la IA**: hoy `ObservadorCompatibilidad`, al recibir `BusquedaCreada`, llena
  `CodigosObjetivo` por compatibilidad de vehículo; la IA lo reemplazará o complementará leyendo
  `Busqueda.TextoLibre`, sin cambiar la firma pública de `ServicioBusquedas.CrearAsync`.
- **Configuración externalizada**: todo se sobreescribe por variable de entorno (`Seccion__Clave`):
  `Catalogo:Modo` (`Local`/`Remoto`), `Catalogo:UrlBaseRemota`, `Persistencia:Proveedor`
  (`InMemory`/`Postgres`), `ConnectionStrings:RepMatch`. Puertos del host en `.env`.
- **Logging/trazabilidad**: cada host llama `UsarSerilog(nombre)` y `UsarCorrelacion()`; el id de
  correlación viaja Web → Catalogo.Api (es parte de las evidencias).
- Base: `EnsureCreatedAsync` + `DatosSemilla`, idempotente, con reintentos. **No hay migraciones.**

### Patrones ya documentados
`docs/patrones.md` cubre Factory, Strategy, Repository, Unit of Work, Adapter, Observer y Facade,
cada uno con *dónde está / qué problema resuelve / qué pasaría sin él / código*. El diagrama de
capas está en `docs/diagramas/capas.md`. Al agregar un patrón, documentarlo con la misma estructura.

## Comandos

```bash
dotnet build && dotnet test                                            # sin Docker, InMemory
dotnet run --project src/RepMatch.Catalogo.Api --urls http://localhost:5081
dotnet run --project src/RepMatch.Web --urls http://localhost:5080    # GET /evidencia/catalogo mide el modo activo
docker compose up --build --wait                                       # web 8080, api 8081, pg 5432 (Web en modo Remoto)
docker compose --profile test run --rm tests                           # pruebas sin SDK
docker compose --profile local up --build web-local                    # Web en modo Local (8090)
bash scripts/evidencias.sh                                             # regenera docs/evidencias/
```

La solución compila **sin advertencias**; mantenerlo así.

## Convenciones

- **Todo en español**: identificadores, comentarios, docs y mensajes de commit. En código, sin
  tildes ni ñ (`Busqueda`, `logica`, `duenio`); en Markdown, con tildes.
- Comentarios XML `/// <summary>` que explican **por qué** (el docente los lee): qué pide la
  consigna, qué alternativa se descartó, qué etapa futura se enchufa ahí.
- Registro de DI por componente con métodos de extensión `Agregar*` / `Usar*`
  (`AgregarPersistencia`, `AgregarAplicacion`, `AgregarCatalogo`, `UsarSerilog`, `UsarCorrelacion`).
- Endpoints de salud en `/salud` en cada host (los usan los healthchecks de Docker).
- Fuentes de ofertas: solo APIs públicas oficiales, nunca scraping.

## Al cambiar cosas, actualizar también

- **Docs**: `docs/informe-arquitectura.md` (incluye la tabla de la sección 7 "cómo recibe las
  entregas siguientes" y la de entregables), `docs/diagramas/*.md`, `docs/patrones.md` y
  `README.md`. Los diagramas son Mermaid y deben renderizar en GitHub.
- **Cantidad de pruebas**: está escrita literal en la sección 5.4 del informe (la salida de
  `dotnet test`) y en `status.md` ("79 passed"). `docker-compose.yml` y
  `tests/RepMatch.Tests/Dockerfile` dicen "las pruebas" sin número, a propósito. Si agregás o quitás
  pruebas, corré `dotnet test` y actualizá los dos.
- **Proyecto nuevo**: sumarlo a `RepMatch.slnx` **y** a los `COPY` de `.csproj` de cada
  `Dockerfile` que lo necesite (`src/*/Dockerfile`, `tests/RepMatch.Tests/Dockerfile`); si no, el
  `dotnet restore` del build en Docker falla.
- Finales de línea: `.gitattributes` fuerza LF en `*.sh`, `Dockerfile` y `*.yml` (corren en Linux).

## Trabajo en paralelo

Puede haber varias sesiones de Claude trabajando issues distintos sobre este mismo working tree.
No cambiar de rama ni crear ramas en el worktree compartido, hacer ediciones puntuales en los docs
compartidos (no reescribirlos enteros), releerlos antes de editar, y no commitear salvo pedido
explícito — y en ese caso, solo los archivos propios.
