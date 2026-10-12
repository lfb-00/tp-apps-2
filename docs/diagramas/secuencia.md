# Diagramas de secuencia

---

## 1. Registrar una búsqueda

Caso de la Primera Parte: el cliente elige un vehículo del garage, describe el problema, y la
búsqueda queda guardada con los códigos de las piezas compatibles. El texto libre se persiste. La
elección de piezas la hace el observador de `BusquedaCreada`, por compatibilidad de vehículo; la
fachada después arma las piezas completas para mostrarlas.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as RepMatch.Web
    participant Fachada as FachadaAplicacion
    participant Busquedas as ServicioBusquedas
    participant Dominio as Busqueda
    participant Repo as IBusquedaRepository
    participant UoW as UnitOfWork
    participant Despacho as DespachadorEventos
    participant Obs as ObservadorCompatibilidad
    participant Catalogo as ICatalogoRepuestos
    participant Vehiculos as ICatalogoVehiculos

    Usuario->>Web: Cliente, vehículo del garage y problema
    alt no eligió vehículo o el texto tiene menos de 15 caracteres
        Web-->>Usuario: "Elegí uno de tus vehículos." o pedido de más detalle, no llama a la fachada
    else datos válidos
        Web->>Fachada: BuscarAsync(CrearBusquedaDto)
        Fachada->>Vehiculos: EsConfiguracionValidaAsync(dto.Vehiculo)
        Note over Fachada,Vehiculos: si no existe devuelve Falla y no crea búsqueda
        Fachada->>Busquedas: CrearAsync
        Note over Busquedas: valida el DTO con FluentValidation y verifica que el cliente exista<br/>(si falla → ResultadoOperacion Falla)<br/>una ExcepcionDominio dentro de CrearAsync también vuelve como Falla
        Busquedas->>Dominio: new Busqueda
        Dominio-->>Busquedas: encola BusquedaCreada
        Note over Busquedas,Dominio: copia EventosDominio antes de guardar
        Busquedas->>Repo: AgregarAsync(busqueda)
        Busquedas->>UoW: ConfirmarAsync
        Note over UoW: la búsqueda queda Pendiente
        Busquedas->>Dominio: LimpiarEventos()
        Busquedas->>Despacho: DespacharAsync
        Despacho->>Obs: ObservarAsync(BusquedaCreada)
        Obs->>Repo: ObtenerPorIdAsync(BusquedaId)
        Note over Obs: si no la encuentra registra un warning<br/>y retorna sin confirmar
        Obs->>Catalogo: BuscarCompatiblesAsync
        alt hay piezas compatibles
            Catalogo-->>Obs: piezas compatibles
            Obs->>Dominio: AsignarCodigosObjetivo
        else sin piezas compatibles
            Obs->>Dominio: Fallar
        else error HTTP o timeout del catálogo remoto (HttpRequestException / TaskCanceledException)
            Obs->>Dominio: Fallar
        end
        Obs->>UoW: ConfirmarAsync
        Busquedas->>Repo: ObtenerPorIdAsync(busqueda.Id)
        Note over Busquedas: vuelve a leer la búsqueda persistida
        Busquedas-->>Fachada: ResultadoOperacion#lt;BusquedaDto#gt;
        alt quedaron códigos asignados
            Fachada->>Catalogo: BuscarCompatiblesAsync (vía ConsultarAsync)
            alt el catálogo responde
                Catalogo-->>Fachada: repuestos del vehículo
                Note over Fachada: arma las piezas de los códigos asignados
                Fachada-->>Web: ResultadoOperacion#lt;ResultadoBusquedaDto#gt; (búsqueda + piezas)
            else falla el catálogo (HttpRequestException / TaskCanceledException)
                Note over Fachada: ConsultarAsync atrapa la excepción<br/>la búsqueda ya quedó guardada, pero no se arman las piezas
                Fachada-->>Web: ResultadoOperacion Falla ("No se pudo consultar el catálogo…")
            end
        else sin códigos (búsqueda Fallida)
            Fachada-->>Web: ResultadoOperacion#lt;ResultadoBusquedaDto#gt; (búsqueda sin piezas)
        end
        Web-->>Usuario: búsqueda con estado y piezas, o mensaje de error
    end
```

---

## 2. Autenticación (login)

El usuario ingresa con email y contraseña. La contraseña nunca se almacena: solo su hash BCrypt.
`SesionActual` guarda el id del cliente en el almacenamiento de sesión del browser, cifrado por
ASP.NET Core Data Protection. El registro de una cuenta nueva usa otro camino desde la misma
pantalla: `RegistrarClienteAsync` (validación FluentValidation, rechazo de email duplicado y
`BCrypt.HashPassword`) y después `EntrarAsync`.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as Login.razor
    participant Fachada as FachadaAplicacion
    participant Clientes as ServicioClientes
    participant Repo as IClienteRepository
    participant BCrypt
    participant Sesion as SesionActual
    participant Storage as ProtectedSessionStorage

    Usuario->>Web: email + contraseña

    alt datos inválidos (email vacío o sin @, contraseña de menos de 8 caracteres)
        Web-->>Usuario: errores en el formulario, no llama a la fachada
    else datos válidos
        Web->>Fachada: AutenticarAsync(email, contraseña)
        Fachada->>Clientes: AutenticarAsync
        Note over Clientes: si email o contraseña vienen vacíos devuelve null
        Clientes->>Repo: ObtenerPorEmailAsync(email)
        Repo-->>Clientes: Cliente con HashContrasena (o null)

        alt email no encontrado o sin hash
            Clientes-->>Fachada: null
            Fachada-->>Web: null
            Web-->>Usuario: "Email o contraseña incorrectos"
        else email encontrado
            Clientes->>BCrypt: Verify(contraseña, hash)
            BCrypt-->>Clientes: true / false

            alt contraseña incorrecta
                Clientes-->>Fachada: null
                Fachada-->>Web: null
                Web-->>Usuario: "Email o contraseña incorrectos"
            else contraseña correcta
                Clientes-->>Fachada: ClienteDto
                Fachada-->>Web: ClienteDto
                Web->>Sesion: EntrarAsync(cliente)
                Sesion->>Storage: SetAsync("clienteId", cliente.Id)
                Note over Sesion: dispara Cambio → MainLayout aplica el tema guardado y actualiza el header
                Web-->>Usuario: redirige a / (Home.OnInitializedAsync carga las búsquedas del cliente)
            end
        end
    end
```

---

## 3. Alta de un vehículo

El cliente agrega un auto a su garage. Las reglas de negocio viven en el dominio: marca y modelo
obligatorios y año entre 1950 y el año actual + 1 (`DatosVehiculo`), vehículo no repetido para el
mismo cliente (`Cliente.AgregarVehiculo`) y VIN válido si se informa (`Vehiculo`; la Web no manda
VIN). `AgregarVehiculoAsync` valida el DTO con FluentValidation, verifica que exista la
configuración en el catálogo y controla que el alias no supere 120 caracteres. Luego busca el cliente,
delega en el dominio y persiste.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as Clientes.razor
    participant Fachada as FachadaAplicacion
    participant Clientes as ServicioClientes
    participant Repo as IClienteRepository
    participant Configuraciones as IConfiguracionVehiculoRepository
    participant Datos as DatosVehiculo
    participant Dominio as Cliente
    participant UoW as UnitOfWork
    participant Sesion as SesionActual

    Usuario->>Web: marca, modelo, año y motor, alias opcional

    alt selección incompleta (marca, modelo, año o motor)
        Web-->>Usuario: "Elegí marca, modelo, año y motor del vehículo.", no llama a la fachada
    else selección completa
        Web->>Fachada: AgregarVehiculoAsync(clienteId, vehiculoDto, alias: alias)
        Fachada->>Clientes: AgregarVehiculoAsync
        Note over Clientes: valida VehiculoDto con FluentValidation<br/>si falla devuelve ResultadoOperacion.Falla
        Clientes->>Configuraciones: ExisteAsync(marca, modelo, anio, motor)
        Note over Clientes,Configuraciones: si no existe devuelve Falla<br/>también rechaza alias de más de 120 caracteres<br/>los pasos siguientes requieren estas validaciones correctas
        Clientes->>Repo: ObtenerPorIdAsync(clienteId)
        Repo-->>Clientes: Cliente con garage actual (o null)

        alt cliente inexistente
            Clientes-->>Fachada: ResultadoOperacion.Falla("No existe el cliente …")
            Fachada-->>Web: error
            Web-->>Usuario: mensaje de error
        else cliente encontrado
            Clientes->>Datos: vehiculo.AEntidad()
            Note over Datos: valida marca y modelo obligatorios y año entre 1950<br/>y el año actual + 1 (el motor es opcional)

            alt datos inválidos
                Datos-->>Clientes: ExcepcionDominio (marca o modelo vacíos, año fuera de rango)
                Note over Clientes: AgregarVehiculo no llega a ejecutarse
                Clientes-->>Fachada: ResultadoOperacion.Falla
                Fachada-->>Web: error
                Web-->>Usuario: mensaje de error
            else datos válidos
                Datos-->>Clientes: DatosVehiculo
                Clientes->>Dominio: AgregarVehiculo(datosVehiculo, vin = null, alias)
                Note over Dominio: rechaza el vehículo repetido<br/>y agrega un Vehiculo al garage

                alt vehículo repetido
                    Dominio-->>Clientes: ExcepcionDominio
                    Clientes-->>Fachada: ResultadoOperacion.Falla
                    Fachada-->>Web: error
                    Web-->>Usuario: mensaje de error
                else vehículo agregado
                    Clientes->>UoW: ConfirmarAsync
                    Note over UoW: persiste el Vehiculo nuevo
                    Clientes-->>Fachada: ResultadoOperacion#lt;ClienteDto#gt;
                    Fachada-->>Web: cliente actualizado con el vehículo nuevo
                    Web->>Sesion: EntrarAsync(resultado.Valor)
                    Note over Sesion: refresca la sesión con el garage nuevo
                    Web-->>Usuario: confirmación y garage actualizado
                end
            end
        end
    end
```

---

## 4. Búsqueda sin sesión

El visitante selecciona un vehículo y describe el problema en Home. El texto se valida en la Web,
pero no se envía a la fachada: esta consulta devuelve piezas, sin crear una `Busqueda`, guardar
historial ni despachar `BusquedaCreada`. La carga de los selectores y la validación del vehículo
usan `ICatalogoVehiculos`, el acceso concreto se elige con la misma fábrica que los repuestos.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as Home.razor
    participant Selector as SelectorVehiculo
    participant Vehiculos as ICatalogoVehiculos
    participant Fachada as FachadaAplicacion
    participant Catalogo as ICatalogoRepuestos

    Web->>Selector: mostrar selector de vehículo
    Selector->>Vehiculos: ListarOpcionesAsync
    alt falla la carga de opciones
        Vehiculos-->>Selector: excepción
        Note over Selector,Web: el selector no captura el error, no se completa la carga
    else opciones disponibles
        Vehiculos-->>Selector: marcas, modelos, rangos de años y motores
        Usuario->>Selector: elegir marca, modelo, año y motor
        Selector-->>Web: vehículo completo o null si falta selección
        Usuario->>Web: describir problema y buscar
        alt vehículo incompleto o texto de menos de 15 caracteres
            Web-->>Usuario: pedido de completar vehículo o ampliar descripción
        else formulario válido
            Web->>Fachada: BuscarRepuestosAsync, sin filtro de sistema
            Note over Web,Fachada: envía solo el vehículo, el texto permanece en la Web
            Fachada->>Vehiculos: EsConfiguracionValidaAsync
            alt error al validar la configuración
                Vehiculos-->>Fachada: excepción
                Note over Fachada,Web: la validación está fuera de ConsultarAsync, el error se propaga
            else configuración inexistente
                Vehiculos-->>Fachada: false
                Fachada-->>Web: Falla, elegir configuración válida
                Web-->>Usuario: mensaje de validación
            else configuración válida
                Vehiculos-->>Fachada: true
                Fachada->>Catalogo: BuscarCompatiblesAsync, vía ConsultarAsync
                alt el catálogo responde
                    Catalogo-->>Fachada: lista de repuestos
                    Fachada-->>Web: Exito con la lista
                    alt hay piezas
                        Web-->>Usuario: fichas de piezas compatibles
                    else lista vacía
                        Web-->>Usuario: aviso de que no hay piezas compatibles
                    end
                else error HTTP o timeout
                    Catalogo-->>Fachada: HttpRequestException o TaskCanceledException
                    Fachada-->>Web: Falla, no se pudo consultar el catálogo
                    Web-->>Usuario: mensaje de error
                else otra excepción
                    Catalogo-->>Fachada: excepción no capturada
                    Note over Fachada,Web: se propaga, no se convierte en ResultadoOperacion
                end
            end
        end
    end
```

Código de referencia: [Home.razor](../../src/RepMatch.Web/Components/Pages/Home.razor),
[SelectorVehiculo.razor](../../src/RepMatch.Web/Components/SelectorVehiculo.razor) y
[FachadaAplicacion.cs](../../src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs).

---

## 5. Consulta de repuestos: acceso local y remoto

El ejemplo es una consulta de compatibilidad desde la fachada. Factory selecciona las estrategias
al arrancar, los consumidores usan los contratos. En modo Remoto, Adapter construye la petición
HTTP y devuelve DTOs. En ambos caminos, Repository consulta los candidatos con EF Core y el
dominio decide la compatibilidad de motor. La API siempre usa los catálogos locales.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as Home o Catalogo.razor
    participant Fabrica as FabricaCatalogo
    participant Fachada as FachadaAplicacion
    participant Vehiculos as ICatalogoVehiculos
    participant Local as CatalogoLocal
    participant Remoto as CatalogoRemoto
    participant Api as RepuestosController
    participant VehiculosApi as CatalogoVehiculosLocal de la API
    participant Repo as IRepuestoRepository
    participant EF as EF Core y base de datos
    participant Dominio as Repuesto

    Note over Fabrica,Remoto: arranque de la Web: registra ambos catálogos y sus dependencias
    Fabrica->>Fabrica: leer Catalogo:Modo
    alt modo Local
        Fabrica-->>Fachada: enlazar contratos con implementaciones locales
    else modo Remoto
        Fabrica-->>Fachada: enlazar contratos con adaptadores HTTP
    end
    Note over Fachada,Vehiculos: ICatalogoVehiculos sigue la misma selección Local o Remoto

    Usuario->>Web: consultar vehículo y sistema opcional
    Web->>Fachada: BuscarRepuestosAsync
    Fachada->>Vehiculos: validar configuración
    Note over Vehiculos: Local consulta IConfiguracionVehiculoRepository<br/>Remoto descarga GET /api/vehiculos/opciones y comprueba la configuración
    alt falla la validación vehicular
        Vehiculos-->>Fachada: excepción
        Note over Fachada,Web: se propaga fuera de ConsultarAsync
    else configuración inexistente
        Vehiculos-->>Fachada: false
        Fachada-->>Web: Falla de validación, sin consultar repuestos
    else configuración válida
        Vehiculos-->>Fachada: true
        alt acceso Local
            Fachada->>Local: BuscarCompatiblesAsync, vía ConsultarAsync
            Local->>Local: convertir vehículo a dominio y sistema a enum
            Local->>Repo: BuscarCompatiblesAsync
            Repo->>EF: consultar aplicaciones por marca, modelo, año y sistema opcional
            EF-->>Repo: candidatos ordenados por código
            loop cada candidato
                Repo->>Dominio: EsCompatibleCon
                Dominio-->>Repo: compatibilidad de marca, modelo, año y motor
            end
            Repo-->>Local: repuestos compatibles
            Local-->>Fachada: DTOs y log del modo Local
        else acceso Remoto
            Fachada->>Remoto: BuscarCompatiblesAsync, vía ConsultarAsync
            Remoto->>Remoto: construir query string y escapar valores
            Remoto->>Api: GET /api/repuestos/compatibles
            Note over Remoto,Api: HttpClient tipado con URL y timeout configurados<br/>el handler propaga la correlación entrante cuando está disponible
            Api->>Api: validar marca, modelo y rango de año
            alt parámetros inválidos
                Api-->>Remoto: HTTP 400 con ProblemDetails
            else parámetros válidos
                Api->>VehiculosApi: EsConfiguracionValidaAsync
                alt configuración inexistente
                    VehiculosApi-->>Api: false
                    Api-->>Remoto: HTTP 400 con ProblemDetails
                else configuración válida
                    VehiculosApi-->>Api: true
                    Api->>Local: BuscarCompatiblesAsync en el proceso de la API
                    Local->>Repo: convertir DTO y consultar compatibles
                    Repo->>EF: consultar candidatos con aplicaciones y filtro opcional
                    EF-->>Repo: candidatos ordenados por código
                    Repo->>Dominio: filtrar con EsCompatibleCon
                    Dominio-->>Repo: candidatos compatibles
                    Repo-->>Local: repuestos
                    Local-->>Api: DTOs
                    Api-->>Remoto: HTTP 200 con JSON
                end
            end
            alt respuesta HTTP exitosa
                Remoto->>Remoto: deserializar JSON y registrar modo Remoto
                Remoto-->>Fachada: lista de DTOs, vacía si el JSON es null
            else respuesta HTTP sin éxito, fallo de red o timeout
                Remoto-->>Fachada: HttpRequestException o TaskCanceledException
                Note over Fachada: ConsultarAsync devuelve Falla con mensaje legible
            end
        end
        Fachada-->>Web: Exito con piezas o Falla de transporte
        Web-->>Usuario: resultados o mensaje de error
    end
```

Las excepciones que no sean HTTP o timeout no las captura `ConsultarAsync`, se propagan.
Esta secuencia es de consulta: no crea búsquedas ni escribe datos. En la búsqueda con cuenta,
el observador y la fachada consumen el mismo contrato, como muestra la secuencia 1.

Código de referencia: [FabricaCatalogo.cs](../../src/RepMatch.Web/Servicios/FabricaCatalogo.cs),
[CatalogoLocal.cs](../../src/RepMatch.Aplicacion/Catalogo/CatalogoLocal.cs),
[CatalogoRemoto.cs](../../src/RepMatch.Clientes.Rest/CatalogoRemoto.cs),
[RepuestosController.cs](../../src/RepMatch.Catalogo.Api/Controllers/RepuestosController.cs) y
[RepuestoRepository.cs](../../src/RepMatch.Persistence/Repositorios/RepuestoRepository.cs).

---

## 6. Eliminación de cuenta

El usuario confirma la baja desde el modal de Perfil con su contraseña. El servicio valida el DTO,
comprueba el cliente y verifica el hash antes de borrar. Con PostgreSQL, Unit of Work ejecuta los
guardados dentro de una sola transacción: primero libera el vehículo predeterminado, si existe,
y después elimina búsquedas y cliente. El garage y las ofertas se eliminan por cascada.

```mermaid
sequenceDiagram
    actor Usuario
    participant Web as Perfil.razor
    participant Fachada as FachadaAplicacion
    participant Clientes as ServicioClientes
    participant Validador as ValidadorEliminarCuenta
    participant RepoClientes as IClienteRepository
    participant BCrypt
    participant Dominio as Cliente
    participant RepoBusquedas as IBusquedaRepository
    participant UoW as UnitOfWork
    participant EF as DbContext y PostgreSQL
    participant Sesion as SesionActual
    participant Storage as ProtectedSessionStorage

    Usuario->>Web: abrir modal y confirmar con contraseña
    alt sin sesión o contraseña vacía
        Note over Web: no llama a la fachada
    else puede confirmar
        Web->>Fachada: EliminarCuentaAsync
        Fachada->>Clientes: EliminarCuentaAsync
        Clientes->>Validador: validar contraseña obligatoria
        alt DTO inválido
            Validador-->>Clientes: errores
            Clientes-->>Fachada: Falla de validación
            Fachada-->>Web: Falla
            Web-->>Usuario: error en el modal
        else DTO válido
            Validador-->>Clientes: validación correcta
            Clientes->>RepoClientes: ObtenerPorIdAsync, incluye garage
            RepoClientes-->>Clientes: cliente o null
            alt cliente inexistente
                Clientes-->>Fachada: Falla, no existe el cliente
                Fachada-->>Web: Falla
                Web-->>Usuario: error en el modal
            else cliente encontrado
                opt existe hash de contraseña
                    Clientes->>BCrypt: Verify
                    BCrypt-->>Clientes: contraseña correcta o incorrecta
                end
                alt sin hash o contraseña incorrecta
                    Clientes-->>Fachada: Falla, contraseña incorrecta
                    Fachada-->>Web: Falla
                    Web-->>Usuario: error, mantiene cuenta y sesión
                else contraseña correcta
                    Clientes->>UoW: EjecutarEnTransaccionAsync
                    UoW->>EF: iniciar transacción dentro de estrategia de ejecución
                    Note over Clientes,UoW: el trabajo siguiente se ejecuta dentro de la transacción
                    opt tiene vehículo predeterminado
                        Clientes->>Dominio: EstablecerVehiculoPredeterminado, null
                        Clientes->>UoW: ConfirmarAsync
                        UoW->>EF: guardar FK del predeterminado liberada
                    end
                    Clientes->>RepoBusquedas: EliminarPorClienteAsync
                    RepoBusquedas->>EF: cargar búsquedas y ofertas, marcar búsquedas para borrar
                    RepoBusquedas-->>Clientes: cantidad de búsquedas
                    Clientes->>RepoClientes: Eliminar
                    RepoClientes->>EF: marcar cliente para borrar
                    Clientes->>UoW: ConfirmarAsync
                    alt guardados correctos
                        UoW->>EF: guardar borrados, cascada de garage y ofertas
                        UoW->>EF: CommitAsync
                        UoW-->>Clientes: trabajo confirmado
                        Clientes-->>Fachada: Exito
                        Fachada-->>Web: Exito
                        Web->>Sesion: guardar aviso de cuenta eliminada y SalirAsync
                        Sesion->>Storage: DeleteAsync, clienteId
                        Note over Sesion: limpia el cliente y dispara Cambio
                        Web-->>Usuario: redirige a Home y muestra Tu cuenta fue eliminada
                    else falla un guardado o el commit
                        EF-->>UoW: excepción
                        Note over UoW,EF: la transacción sin commit se descarta y revierte los cambios<br/>la estrategia puede reintentar los errores transitorios
                        Note over Clientes,Web: la excepción se propaga, no se ejecuta el cierre de sesión por éxito
                    end
                end
            end
        end
    end
```

La transacción del diagrama corresponde a PostgreSQL. Con InMemory, `EjecutarEnTransaccionAsync`
ejecuta el trabajo directamente, sin transacción ni rollback entre guardados.

Código de referencia: [Perfil.razor](../../src/RepMatch.Web/Components/Pages/Perfil.razor),
[ServicioClientes.cs](../../src/RepMatch.Aplicacion/Servicios/ServicioClientes.cs),
[UnitOfWork.cs](../../src/RepMatch.Persistence/UnitOfWork.cs),
[BusquedaRepository.cs](../../src/RepMatch.Persistence/Repositorios/BusquedaRepository.cs) y
[SesionActual.cs](../../src/RepMatch.Web/Servicios/SesionActual.cs).
