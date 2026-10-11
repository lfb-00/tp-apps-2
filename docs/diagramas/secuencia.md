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
