# Patrones de diseño aplicados en RepMatch

Este documento describe los patrones de diseño implementados en el código. Para cada uno se indica
en qué archivo y clase vive, qué problema concreto resuelve dentro de RepMatch, qué alternativa se
descartó y cómo se ve en el código real.

| Patrón | Dónde vive |
|---|---|
| [Factory](#factory-fabricacatalogo) | `src/RepMatch.Web/Servicios/FabricaCatalogo.cs` |
| [Strategy](#strategy-icatalogorepuestos-con-dos-implementaciones) | `src/RepMatch.Contracts/ICatalogoRepuestos.cs`, con `CatalogoLocal` y `CatalogoRemoto` |
| [Repository](#repository-irepository-con-implementaciones-en-persistence) | `src/RepMatch.Domain/Repositorios/` y `src/RepMatch.Persistence/Repositorios/` |
| [Unit of Work](#unit-of-work-iunitofwork-y-unitofwork) | `src/RepMatch.Domain/Repositorios/IUnitOfWork.cs` y `src/RepMatch.Persistence/UnitOfWork.cs` |
| [Adapter](#adapter-catalogoremoto) | `src/RepMatch.Clientes.Rest/CatalogoRemoto.cs` |
| [Observer](#observer-despachador-de-eventos-de-dominio) | `src/RepMatch.Aplicacion/Eventos/` — `IDespachadorEventos`, `DespachadorEventos`, `IObservadorEventoDominio`, `ObservadorCompatibilidad`; el evento `BusquedaCreada` en `src/RepMatch.Domain/Eventos/` |
| [Facade](#facade-fachadaaplicacion) | `src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs` |

---

## Factory: `FabricaCatalogo`

### Dónde está

`src/RepMatch.Web/Servicios/FabricaCatalogo.cs`, clase estática `FabricaCatalogo`, método de
extensión `AgregarCatalogo`.

### Qué problema resuelve

RepMatch consulta el catálogo de repuestos de dos maneras distintas. En modo local lo hace por
invocación directa en proceso, contra los repositorios. En modo remoto lo hace por HTTP contra
`Catalogo.Api`. La decisión de cuál de las dos queda activa se toma una sola vez, en el arranque,
leyendo la clave `Catalogo:Modo` de la configuración, y no se filtra a ningún consumidor.

El problema no es solamente elegir entre dos clases. Los dos caminos necesitan cadenas de
dependencias distintas: el local arrastra el `DbContext`, los tres repositorios y la unidad de
trabajo, mientras que el remoto necesita un `HttpClient` tipado con su URL base y la propagación de
correlación. Un registro directo de la interfaz no alcanza, porque antes de elegir hay que construir
dos cosas diferentes.

Por eso la fábrica registra siempre las dos clases concretas, cada una con su cadena de
dependencias, y enlaza la interfaz con un delegado que resuelve la que indique la configuración. Las
dos quedan disponibles en el contenedor y una queda elegida. La comparación entre los dos caminos
se hace fuera de la interfaz de usuario: `scripts/evidencias.sh` levanta la Web una vez en cada modo
y mide `GET /evidencia/catalogo`, y `AccesoLocalVsRemotoTests` verifica que las dos implementaciones
devuelvan exactamente lo mismo.

En cuanto a la forma, la fábrica no expone un método `Crear` sino que registra un delegado en el
contenedor de dependencias. Es la manera idiomática de aplicar el patrón en .NET, donde la
construcción de objetos ya está delegada en el contenedor. El punto de decisión sigue siendo único
y explícito.

### Qué pasaría sin él

La alternativa que se descartó era que cada consumidor leyera `Catalogo:Modo` y decidiera por su
cuenta. Eso significa repetir el mismo `if` en `FachadaAplicacion`, `ObservadorCompatibilidad` y el
endpoint `GET /evidencia/catalogo` de `src/RepMatch.Web/Program.cs`, es decir tres lugares que habría
que tocar de nuevo cada vez que se agregue una forma de acceso. Además cada consumidor pasaría a
depender de las dos clases concretas y de la configuración en lugar de la interfaz, y la decisión
de modo, que hoy se toma una sola vez en el arranque, quedaría repartida entre todos ellos.

La otra opción era elegir en tiempo de compilación con directivas `#if`, pero obliga a recompilar
para cambiar de modo, y eso anula la posibilidad de configurar el comportamiento por variable de
entorno.

### Código

```csharp
// src/RepMatch.Web/Servicios/FabricaCatalogo.cs (fragmento de AgregarCatalogo)
var opciones = configuracion.GetSection(OpcionesCatalogo.Seccion).Get<OpcionesCatalogo>()
               ?? new OpcionesCatalogo();

// Camino LOCAL: referencia de proyecto contra la capa de datos, sin red.
servicios.AgregarPersistencia(configuracion);
servicios.AddScoped<CatalogoLocal>();

// Camino REMOTO: HttpClient tipado contra Catalogo.Api.
servicios.AgregarCatalogoRemoto(opciones);

// La interfaz queda enlazada al camino que indique la configuracion.
servicios.AddScoped<ICatalogoRepuestos>(sp => opciones.Modo switch
{
    ModoAccesoCatalogo.Remoto => sp.GetRequiredService<CatalogoRemoto>(),
    _ => sp.GetRequiredService<CatalogoLocal>()
});
```

Cambiar `Catalogo__Modo=Local` por `Catalogo__Modo=Remoto` reconfigura toda la aplicación sin
recompilar y sin que ningún consumidor cambie.

---

## Strategy: `ICatalogoRepuestos` con dos implementaciones

### Dónde está

El contrato en `src/RepMatch.Contracts/ICatalogoRepuestos.cs`. Las dos implementaciones en
`src/RepMatch.Aplicacion/Catalogo/CatalogoLocal.cs` y `src/RepMatch.Clientes.Rest/CatalogoRemoto.cs`.

### Qué problema resuelve

El mecanismo de acceso al catálogo es una cuestión de infraestructura y no de negocio.
`ObservadorCompatibilidad` necesita saber qué repuestos son compatibles con un vehículo para decidir
los códigos objetivo de una búsqueda, y `FachadaAplicacion` necesita listar y buscar repuestos para
la presentación (`BuscarAsync`, `BuscarRepuestosAsync`, `ListarRepuestosAsync`). Esa respuesta no
cambia según venga de EF Core o de una llamada HTTP a otro proceso.

Strategy es lo que permite mover esa pieza sin mover nada más. Las dos implementaciones cumplen el
mismo contrato, devuelven los mismos DTOs y son intercambiables. El consumidor no las distingue.

Hay una excepción deliberada en el contrato, que es la propiedad `Modo`. En rigor una estrategia no
debería anunciar cuál es, porque rompe parte de la indistinguibilidad. Está ahí porque el log de
cada operación registra por qué camino se resolvió, y esa información hace falta para diagnosticar
problemas y para comparar el rendimiento de los dos modos. Es una decisión tomada a conciencia.

### Qué pasaría sin él

Sin la interfaz, `ObservadorCompatibilidad` y `FachadaAplicacion` recibirían cada uno un
`IRepuestoRepository` y un `HttpClient`, y tendrían adentro la rama que decide cuál usar. La lógica de
negocio quedaría mezclada con la de transporte, y cada prueba de esas clases tendría que montar las
dos dependencias aunque solo ejercite una.

El costo se ve mejor pensando en una tercera forma de acceso. Con Strategy, agregarla es escribir una
clase nueva y sumar una rama en la fábrica. Sin Strategy, hay que tocar cada consumidor que tenga la
decisión duplicada.

### Código

```csharp
// src/RepMatch.Contracts/ICatalogoRepuestos.cs (fragmento)
public interface ICatalogoRepuestos
{
    /// <summary>Como se esta accediendo al componente ("Local", "Remoto", ...). Se registra en el
    /// log de cada operacion: es la evidencia que pide el entregable.</summary>
    string Modo { get; }

    /// <summary>Repuestos del catalogo compatibles con el vehiculo indicado.</summary>
    /// <param name="sistema">Filtro opcional por sistema (Frenos, Motor, ...).</param>
    Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default);

    Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default);

    Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default);
}
```

El consumidor ve una sola cosa, que es la interfaz. `ObservadorCompatibilidad` recibe
`ICatalogoRepuestos` sin saber cuál de las dos estrategias le inyectó el contenedor, y la usa igual
en los dos modos:

```csharp
// src/RepMatch.Aplicacion/Eventos/ObservadorCompatibilidad.cs (fragmento: constructor)
public sealed class ObservadorCompatibilidad(
    IBusquedaRepository busquedas,
    ICatalogoRepuestos catalogo,
    IUnitOfWork unidadDeTrabajo,
    ILogger<ObservadorCompatibilidad> log) : IObservadorEventoDominio
```

```csharp
// src/RepMatch.Aplicacion/Eventos/ObservadorCompatibilidad.cs (fragmento de ObservarAsync)
var compatibles = await catalogo.BuscarCompatiblesAsync(creada.Vehiculo.ADto(), sistema: null, ct);
```

| | Local | Remoto |
|---|---|---|
| Implementación | `Aplicacion.Catalogo.CatalogoLocal` | `Clientes.Rest.CatalogoRemoto` |
| Mecanismo | invocación directa en proceso | HTTP + JSON contra `Catalogo.Api` |
| Serialización | ninguna | JSON de ida y vuelta |
| Latencia en régimen (mediana) | ~2 ms | ~7 ms |

---

## Repository: `I*Repository` con implementaciones en Persistence

### Dónde está

Las interfaces en `src/RepMatch.Domain/Repositorios/`, que son `IClienteRepository`,
`IRepuestoRepository` e `IBusquedaRepository`. Las implementaciones en
`src/RepMatch.Persistence/Repositorios/`.

### Qué problema resuelve

El dominio tiene reglas que conviene probar por separado: la compatibilidad de un repuesto con un
vehículo en `Repuesto.EsCompatibleCon`, las transiciones de estado de una `Busqueda` y la
comparación de precios dentro de una misma moneda, que aplican `Busqueda.OfertasOrdenadasPorPrecio`
y el value object `Dinero`. Si esas entidades dependieran de EF
Core, cada prueba necesitaría levantar un contexto y un proveedor de datos.

Acá el patrón se usa sobre todo por la dirección de la dependencia. Las interfaces se declaran en
`RepMatch.Domain`, que no referencia ningún otro proyecto, y `RepMatch.Persistence` las implementa.
Por eso en el diagrama de componentes la flecha que va de `Persistence` a `Domain` dice implementa y
no usa.

Hay un segundo beneficio, visible en `RepuestoRepository.BuscarCompatiblesAsync`. El repositorio es
el lugar donde se reparte la búsqueda entre lo que el motor puede traducir a SQL con índice, que son
marca, modelo y rango de años, y la regla fina de compatibilidad que vive en la entidad. Ese reparto
necesita un lugar propio, porque sin repositorio queda disperso entre los servicios.

### Qué pasaría sin él

La alternativa descartada era inyectar `RepMatchDbContext` directamente en los servicios de
aplicación y escribir las consultas LINQ ahí. Las consecuencias concretas son tres:

- `RepMatch.Aplicacion` pasaría a referenciar EF Core, con lo cual el proveedor de datos entra en la
  capa de negocio.
- Las consultas con `Include` se repetirían en cada servicio que necesite una `Busqueda` con sus
  ofertas, con el riesgo de que a alguna le falte el `Include` y devuelva la colección vacía sin
  dar error.
- La decisión sobre qué filtra la base y qué filtra la entidad quedaría tomada caso por caso, sin un
  criterio único.

También se evaluó un repositorio genérico del tipo `IRepositorio<T>` y se descartó. Cada agregado
necesita consultas propias, como `ListarRecientesAsync`, `BuscarCompatiblesAsync` y
`ObtenerPorCodigoAsync`, y un repositorio genérico termina exponiendo `IQueryable`, lo que equivale
a volver a armar consultas de EF desde la capa de arriba.

### Código

La interfaz, en el dominio, sin una sola referencia a EF:

```csharp
// src/RepMatch.Domain/Repositorios/IBusquedaRepository.cs
public interface IBusquedaRepository
{
    Task<Busqueda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarRecientesAsync(int cantidad = 20, CancellationToken ct = default);
    Task AgregarAsync(Busqueda busqueda, CancellationToken ct = default);

    /// <summary>Marca para borrar todas las busquedas del cliente. Se confirma con la unidad de
    /// trabajo: busquedas.ClienteId no tiene FK a clientes, asi que no caen por cascada.</summary>
    Task<int> EliminarPorClienteAsync(Guid clienteId, CancellationToken ct = default);
}
```

La implementación, en persistencia, donde sí vive EF y el reparto entre SQL y dominio. El filtro
grueso (marca, modelo, rango de años y, si se pidió, sistema) lo hace la base; la regla fina del
motor la aplica la entidad con `EsCompatibleCon` sobre los candidatos ya traídos:

```csharp
// src/RepMatch.Persistence/Repositorios/RepuestoRepository.cs (fragmento de BuscarCompatiblesAsync)
var consulta = contexto.Repuestos
    .Include(r => r.Aplicaciones)
    .Where(r => r.Aplicaciones.Any(a =>
        a.Marca.ToLower() == vehiculo.Marca.ToLower()
        && a.Modelo.ToLower() == vehiculo.Modelo.ToLower()
        && a.AnioDesde <= vehiculo.Anio
        && a.AnioHasta >= vehiculo.Anio));

if (sistema is not null)
    consulta = consulta.Where(r => r.Sistema == sistema.Value);

var candidatos = await consulta.OrderBy(r => r.CodigoCanonico).ToListAsync(ct);

return [.. candidatos.Where(r => r.EsCompatibleCon(vehiculo))];
```

---

## Unit of Work: `IUnitOfWork` y `UnitOfWork`

### Dónde está

`src/RepMatch.Domain/Repositorios/IUnitOfWork.cs` y `src/RepMatch.Persistence/UnitOfWork.cs`.

### Qué problema resuelve

Cada operación de negocio acumula sus cambios en los repositorios y los confirma juntos, en una sola
llamada a `ConfirmarAsync`. Crear una búsqueda son dos operaciones, cada una con su propia
confirmación: `ServicioBusquedas.CrearAsync` guarda la `Busqueda` en estado `Pendiente` y emite
`BusquedaCreada`; después `ObservadorCompatibilidad` le asigna los códigos objetivo, o la marca
`Fallida` si la consulta al catálogo falla con `HttpRequestException` o `TaskCanceledException`
(error HTTP o timeout del catálogo remoto), y confirma de nuevo. Están separadas a propósito, para
que la búsqueda no se pierda si falla el catálogo: ante otra excepción (por ejemplo, de base de
datos en modo Local) la búsqueda queda `Pendiente` y la excepción se propaga a `CrearAsync`, que la
devuelve como `Falla` si es `ExcepcionDominio` y si no la deja subir (ver
[Observer](#observer-despachador-de-eventos-de-dominio)).

Cuando una sola operación necesita varias confirmaciones y tienen que quedar todas o ninguna, se usa
`IUnitOfWork.EjecutarEnTransaccionAsync`. Es el caso de `ServicioClientes.EliminarCuentaAsync`:
`clientes` apunta al vehículo predeterminado y `vehiculos` apunta al cliente, y EF no puede ordenar
el borrado de ese ciclo de FKs en un solo guardado. Por eso primero suelta el vehículo predeterminado
y confirma, y después borra las búsquedas y el cliente y confirma otra vez, todo dentro de la misma
transacción. Con el proveedor InMemory, que no tiene transacciones, el método ejecuta el trabajo
directamente.

`DbContext` ya funciona como unidad de trabajo, porque rastrea los cambios y los confirma en una
transacción. La interfaz existe para que los servicios de aplicación puedan pedir la confirmación
sin conocer EF Core. Si el servicio llamara directamente a `contexto.SaveChangesAsync`,
`RepMatch.Aplicacion` tendría que referenciar EF y la inversión de dependencias se rompería por ahí.

`ConfirmarAsync` es un envoltorio de una línea. No está por la lógica que agrega sino por el
acoplamiento que evita.

### Qué pasaría sin él

La alternativa descartada era que cada repositorio guardara por su cuenta, con un `SaveChanges`
dentro de `AgregarAsync`, `Eliminar` o `EliminarPorClienteAsync`. Eso rompe la atomicidad: en la
baja de cuenta podrían borrarse las búsquedas del cliente y fallar después el borrado del cliente,
con lo cual queda una cuenta activa sin su historial y nada que lo deshaga. Además multiplica los
viajes a la base, porque una operación de negocio pasa a ser varias transacciones.

La otra alternativa era inyectar el `DbContext` en la capa de aplicación. Resuelve la atomicidad
pero deja el acoplamiento que se describe arriba.

### Código

```csharp
// src/RepMatch.Domain/Repositorios/IUnitOfWork.cs, declarado en el dominio
public interface IUnitOfWork
{
    Task<int> ConfirmarAsync(CancellationToken ct = default);

    /// <summary>Corre varias confirmaciones como una sola transaccion: o quedan todas o ninguna.
    /// Hace falta cuando un cambio no se puede ordenar en un solo guardado.</summary>
    Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct = default);
}
```

```csharp
// src/RepMatch.Persistence/UnitOfWork.cs, implementado sobre EF Core
public sealed class UnitOfWork(RepMatchDbContext contexto) : IUnitOfWork
{
    public Task<int> ConfirmarAsync(CancellationToken ct = default) =>
        contexto.SaveChangesAsync(ct);

    public async Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // El proveedor InMemory (pruebas y modo sin Docker) no tiene transacciones.
        if (!contexto.Database.IsRelational())
        {
            await trabajo(ct);
            return;
        }

        var estrategia = contexto.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
            await trabajo(ct);
            await transaccion.CommitAsync(ct);
        });
    }
}
```

En la baja de cuenta, las dos confirmaciones corren dentro de una sola transacción, así que si falla
el borrado del cliente tampoco se borran sus búsquedas ni se suelta su vehículo predeterminado:

```csharp
// src/RepMatch.Aplicacion/Servicios/ServicioClientes.cs (fragmento de EliminarCuentaAsync)
await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
{
    // clientes -> vehiculo predeterminado y vehiculos -> cliente forman un ciclo: EF no
    // puede ordenar el DELETE de los dos en un solo guardado. Se suelta primero la FK del
    // predeterminado, dentro de la misma transaccion.
    if (cliente.VehiculoPredeterminadoId is not null)
    {
        cliente.EstablecerVehiculoPredeterminado(null);
        await unidadDeTrabajo.ConfirmarAsync(token);
    }

    cantidadBusquedas = await busquedas.EliminarPorClienteAsync(clienteId, token);
    clientes.Eliminar(cliente);
    await unidadDeTrabajo.ConfirmarAsync(token);
}, ct);
```

---

## Adapter: `CatalogoRemoto`

### Dónde está

`src/RepMatch.Clientes.Rest/CatalogoRemoto.cs`.

### Qué problema resuelve

`Catalogo.Api` e `ICatalogoRepuestos` hablan lenguajes distintos. La API expone query strings con
valores escapados, cuerpos JSON y códigos de estado HTTP. El contrato del dominio habla de
`VehiculoDto`, listas de `RepuestoDto` y `null` cuando algo no existe. `CatalogoRemoto` es el que
traduce entre los dos.

Las traducciones concretas que hace son cuatro:

- Arma la ruta a partir de un `VehiculoDto` y escapa cada valor, porque una marca con espacios
  rompería la URL.
- Convierte el 404 en `null`. Es la traducción principal, porque "no encontrado" es un código de
  estado del lado HTTP y un valor ausente del lado del contrato.
- Convierte el resto de los errores en excepción, con `EnsureSuccessStatusCode`.
- Convierte un cuerpo JSON vacío o nulo en lista vacía y no en `null`.

Conviene distinguirlo de Strategy, porque los dos apuntan a la misma clase. Strategy es la decisión
de tener dos implementaciones intercambiables del catálogo. Adapter es lo que `CatalogoRemoto` hace
internamente, que es acomodar un protocolo ajeno a una interfaz propia. Es una misma clase cumpliendo
dos roles distintos.

### Qué pasaría sin él

Sin el adaptador, el `HttpClient` se inyectaría en los consumidores del catálogo
(`FachadaAplicacion`, `ObservadorCompatibilidad` y el endpoint `GET /evidencia/catalogo`) y cada uno
tendría que armar la query string, revisar el `StatusCode`, deserializar y decidir qué hacer con un
404. Además el modo local dejaría de ser intercambiable con el remoto, porque los consumidores ya
estarían escritos contra HTTP.

Queda una filtración conocida y acotada. `FachadaAplicacion.ConsultarAsync` y
`ObservadorCompatibilidad` atrapan `HttpRequestException` y `TaskCanceledException`: la fachada las
convierte en un `ResultadoOperacion` con un mensaje entendible para la página, y el observador marca
la búsqueda como `Fallida`. Ninguna página Razor atrapa esas excepciones. El adaptador traduce los
errores de protocolo pero deja pasar los de transporte. Sin adaptador, la
filtración no sería una excepción puntual sino todo el modelo HTTP.

### Código

En `ObtenerPorCodigoAsync` se ven las dos traducciones de errores: si la API responde 404, el
método registra el caso en el log y devuelve `null`, que es como el contrato expresa "no existe";
cualquier otro código de error lo convierte en excepción con `EnsureSuccessStatusCode`:

```csharp
// src/RepMatch.Clientes.Rest/CatalogoRemoto.cs (fragmento)
public sealed class CatalogoRemoto(HttpClient http, ILogger<CatalogoRemoto> log) : ICatalogoRepuestos
{
    public string Modo => "Remoto";

    public async Task<RepuestoDto?> ObtenerPorCodigoAsync(
        string codigoCanonico, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoCanonico);

        var respuesta = await http.GetAsync(
            $"api/repuestos/{Uri.EscapeDataString(codigoCanonico)}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> no encontrado", Modo, codigoCanonico);
            return null;
        }

        respuesta.EnsureSuccessStatusCode();

        var repuesto = await respuesta.Content.ReadFromJsonAsync<RepuestoDto>(ct);

        log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> encontrado", Modo, codigoCanonico);
        return repuesto;
    }
}
```

Del otro lado, `CatalogoLocal` cumple el mismo contrato sin traducir nada, porque no hay ningún
protocolo ajeno del que defenderse:

```csharp
// src/RepMatch.Aplicacion/Catalogo/CatalogoLocal.cs (fragmento)
public async Task<RepuestoDto?> ObtenerPorCodigoAsync(
    string codigoCanonico, CancellationToken ct = default)
{
    var repuesto = await repuestos.ObtenerPorCodigoAsync(codigoCanonico, ct);

    log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> {Resultado}",
        Modo, codigoCanonico, repuesto is null ? "no encontrado" : "encontrado");

    return repuesto?.ADto();
}
```

La asimetría entre las dos implementaciones es el patrón Adapter: todo lo que `CatalogoRemoto` hace
de más es traducción.

---

## Observer: despachador de eventos de dominio

### Dónde está

El contrato del evento en `src/RepMatch.Domain/Comun/IEventoDominio.cs`. El buzón de eventos en
`src/RepMatch.Domain/Comun/EntidadBase.cs` (propiedad `EventosDominio`, método `RegistrarEvento`).
El evento concreto en `src/RepMatch.Domain/Eventos/BusquedaCreada.cs`. La interfaz de observador en
`src/RepMatch.Aplicacion/Eventos/IObservadorEventoDominio.cs`. El observador concreto en
`src/RepMatch.Aplicacion/Eventos/ObservadorCompatibilidad.cs`. El despachador en
`src/RepMatch.Aplicacion/Eventos/IDespachadorEventos.cs` y `DespachadorEventos.cs`.

### Qué problema resuelve

Cuando el usuario envía una búsqueda, hay dos cosas que hacer: guardar la búsqueda y averiguar qué
repuestos son compatibles con el vehículo. La segunda depende del catálogo, que es una pieza de
infraestructura. Si `ServicioBusquedas` llamara al catálogo directamente, el servicio que crea
pedidos pasaría a conocer el mecanismo de resolución de piezas, que es una responsabilidad aparte.
Y si en el futuro hubiera que enviar una notificación o escribir en una cola de auditoría después
de crear una búsqueda, habría que tocar el mismo servicio cada vez.

Observer separa el hecho de que algo ocurrió del conjunto de reacciones que ese hecho dispara.
`Busqueda` registra el evento `BusquedaCreada` en su propio constructor, sin saber quién lo
escucha. `ServicioBusquedas` extrae los eventos acumulados, persiste la entidad, limpia los
eventos y llama al despachador. `DespachadorEventos` entrega el evento a todos los observadores
registrados que respondan `true` en `PuedeObservar`. `ObservadorCompatibilidad` recibe el evento,
consulta el catálogo y asigna los códigos a la búsqueda en una segunda transacción.

El orden importa: el despacho ocurre después de `ConfirmarAsync`. Así el observador encuentra la
búsqueda en la base de datos cuando la lee. Si la consulta al catálogo falla con
`HttpRequestException` o `TaskCanceledException` (error HTTP o timeout del catálogo remoto), o si no
devuelve repuestos compatibles, `ObservadorCompatibilidad` llama a `busqueda.Fallar(motivo)` y
confirma, con lo cual la búsqueda queda en estado `Fallida` y con el motivo registrado. Ante
cualquier otra excepción (por ejemplo, un error de base de datos en modo Local) la búsqueda queda en
estado `Pendiente`, pero no se pierde, porque ya se había confirmado antes del despacho. Ese error
no se atrapa en el observador ni en `DespachadorEventos`, así que se propaga a `CrearAsync`: si es una `ExcepcionDominio`, vuelve como
`ResultadoOperacion` con `Falla`; si es otra excepción, sube sin atrapar.

Este diseño también es el punto de extensión hacia RabbitMQ. En la Segunda Parte se registrará
otra implementación de `IDespachadorEventos` que, en lugar de invocar observadores en proceso,
publique `BusquedaCreada` a través de `IEventBus`, la interfaz propia que envolverá
`RabbitMQ.Client`. `ServicioBusquedas` no cambia.

### Qué pasaría sin él

La alternativa descartada era que `ServicioBusquedas` recibiera `ICatalogoRepuestos` como
dependencia directa y llamara a `BuscarCompatiblesAsync` justo después de guardar la búsqueda. Las
consecuencias concretas son tres:

- El servicio de búsquedas acopla dos responsabilidades: registrar el pedido y resolver las piezas.
  Si el catálogo cambia de interfaz, `ServicioBusquedas` también cambia.
- Agregar un segundo efecto colateral —por ejemplo, publicar en una cola o mandar un email—
  requiere tocar `ServicioBusquedas`, que ya tiene su propia lógica de validación, persistencia y
  manejo de errores.
- Migrar a mensajería asincrónica (RabbitMQ) obligaría a reescribir el servicio. Con
  `IDespachadorEventos` alcanza con registrar otra implementación del despachador, que publique
  `BusquedaCreada` a través de `IEventBus`, la interfaz propia que envolverá `RabbitMQ.Client`.

### Código

El dominio registra el evento en el constructor de la entidad, sin saber quién lo escucha:

```csharp
// src/RepMatch.Domain/Entidades/Busqueda.cs (constructor)
RegistrarEvento(new BusquedaCreada(Id, ClienteId, Vehiculo, TextoLibre));
```

`ServicioBusquedas` captura los eventos antes de persistir y los despacha recién después de
confirmar. El fragmento omite la validación del DTO, la verificación de que el cliente exista, el
`try`/`catch` de `ExcepcionDominio` y la relectura final de la búsqueda:

```csharp
// src/RepMatch.Aplicacion/Servicios/ServicioBusquedas.cs (fragmento de CrearAsync)
var busqueda = new Busqueda(dto.ClienteId, dto.Vehiculo.AEntidad(), dto.TextoLibre);
var pendientes = busqueda.EventosDominio.ToArray();

await busquedas.AgregarAsync(busqueda, ct);
await unidadDeTrabajo.ConfirmarAsync(ct);
busqueda.LimpiarEventos();

await despachador.DespacharAsync(pendientes, ct);
```

El despachador itera los observadores registrados y delega solo a los que aceptan el evento. El
fragmento omite el `ArgumentNullException.ThrowIfNull(eventos)` inicial y el log de cada entrega:

```csharp
// src/RepMatch.Aplicacion/Eventos/DespachadorEventos.cs (fragmento de DespacharAsync)
foreach (var evento in eventos)
{
    foreach (var observador in observadores)
    {
        if (!observador.PuedeObservar(evento))
            continue;

        await observador.ObservarAsync(evento, ct);
    }
}
```

El observador reacciona al hecho sin que nadie se lo pida directamente. La confirmación del final
es una segunda transacción, independiente de la que guardó la búsqueda:

```csharp
// src/RepMatch.Aplicacion/Eventos/ObservadorCompatibilidad.cs
public bool PuedeObservar(IEventoDominio evento) => evento is BusquedaCreada;

public async Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default)
{
    if (evento is not BusquedaCreada creada)
        return;

    var busqueda = await busquedas.ObtenerPorIdAsync(creada.BusquedaId, ct);
    if (busqueda is null)
    {
        log.LogWarning(
            "No se encontró la búsqueda {BusquedaId} al observar BusquedaCreada",
            creada.BusquedaId);
        return;
    }

    try
    {
        var compatibles = await catalogo.BuscarCompatiblesAsync(creada.Vehiculo.ADto(), sistema: null, ct);

        if (compatibles.Count > 0)
            busqueda.AsignarCodigosObjetivo(compatibles.Select(r => r.CodigoCanonico));
        else
            busqueda.Fallar($"Todavía no hay repuestos catalogados para un {creada.Vehiculo}.");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        busqueda.Fallar("No se pudo consultar el catálogo para completar la búsqueda.");
        log.LogWarning(ex, "Falló el catálogo al completar la búsqueda {BusquedaId}", creada.BusquedaId);
    }

    await unidadDeTrabajo.ConfirmarAsync(ct);

    log.LogInformation(
        "Búsqueda {BusquedaId} completada por compatibilidad: estado {Estado}, {Codigos} códigos",
        busqueda.Id, busqueda.Estado, busqueda.CodigosObjetivo.Count);
}
```

El registro en DI usa `AddScoped` con la interfaz, de modo que el contenedor inyecta
automáticamente todos los `IObservadorEventoDominio` registrados al `DespachadorEventos`:

```csharp
// src/RepMatch.Aplicacion/ExtensionesAplicacion.cs (fragmento)
servicios.AddScoped<IObservadorEventoDominio, ObservadorCompatibilidad>();
servicios.AddScoped<IDespachadorEventos, DespachadorEventos>();
```

---

## Facade: `FachadaAplicacion`

### Dónde está

`src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs`. Registrada en DI dentro de
`src/RepMatch.Aplicacion/ExtensionesAplicacion.cs` junto con los servicios que envuelve.

### Qué problema resuelve

La capa de presentación necesita interactuar con tres piezas de la capa de aplicación:
`ServicioClientes`, `ServicioBusquedas` e `ICatalogoRepuestos`. Sin una fachada, cada página Razor
inyectaría esas tres dependencias por separado. El problema no es solo el número de `@inject`:
las páginas pasarían a conocer la estructura interna de la capa de aplicación, qué servicio maneja
qué operación y qué interfaz expone el catálogo. Cualquier reorganización interna se trasladaría
a todas las páginas.

Hay además una operación compuesta que ninguno de los tres componentes puede dar por sí solo.
Cuando el usuario busca repuestos, la presentación espera recibir en un solo llamado tanto la
búsqueda persistida como las piezas del catálogo resueltas. Eso requiere coordinar
`ServicioBusquedas.CrearAsync` con `ICatalogoRepuestos.BuscarCompatiblesAsync` y manejar el caso
en que el catálogo falle con `HttpRequestException` o `TaskCanceledException` (error HTTP o timeout
del catálogo remoto). Si dentro de `CrearAsync` el observador recibe otra excepción (por ejemplo, de
base de datos en modo Local), la búsqueda queda `Pendiente` y la excepción se propaga a `CrearAsync`,
que la devuelve como `Falla` si es `ExcepcionDominio` y si no la deja subir.
`FachadaAplicacion.BuscarAsync` encapsula esa orquestación y la expone como un único método con
resultado tipado.

La fachada también centraliza el manejo de errores del catálogo para las operaciones de consulta.
El método privado `ConsultarAsync` convierte `HttpRequestException` y `TaskCanceledException` en
`ResultadoOperacion` con mensaje legible, sin que ninguna página tenga que atrapar esas
excepciones.

### Qué pasaría sin él

La alternativa descartada era que cada página Razor inyectara los servicios que necesita. Las
consecuencias concretas son tres:

- `Home.razor`, `Login.razor`, `Clientes.razor`, `Perfil.razor` y `Catalogo.razor` inyectarían
  `ServicioClientes`, `ServicioBusquedas` o `ICatalogoRepuestos` según lo que usen. Mover una operación de un servicio
  a otro obliga a actualizar todas las páginas que la usan.
- La operación `BuscarAsync` —crear la búsqueda y resolver las piezas en un solo llamado— no
  tiene un lugar natural. Quedaría duplicada en cada página que la necesite, o repartida en dos
  llamados separados con el manejo de errores repetido.
- Cuando en la siguiente etapa se agreguen servicios nuevos (mensajería, IA), las páginas tendrían
  que absorber esas dependencias adicionales.

### Código

La fachada recibe los tres componentes internos y no los expone hacia afuera:

```csharp
// src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs (fragmento)
public sealed class FachadaAplicacion(
    ServicioClientes clientes,
    ServicioBusquedas busquedas,
    ICatalogoRepuestos catalogo)
```

La operación compuesta crea la búsqueda, verifica que tenga códigos objetivo asignados y luego
resuelve las piezas del catálogo en un segundo paso:

```csharp
// src/RepMatch.Aplicacion/Fachada/FachadaAplicacion.cs
public async Task<ResultadoOperacion<ResultadoBusquedaDto>> BuscarAsync(
    CrearBusquedaDto dto, CancellationToken ct = default)
{
    var creada = await busquedas.CrearAsync(dto, ct);
    if (creada.EsFallido)
        return ResultadoOperacion<ResultadoBusquedaDto>.Falla(creada.Errores);

    if (creada.Valor.CodigosObjetivo.Count == 0)
        return ResultadoOperacion<ResultadoBusquedaDto>.Exito(new ResultadoBusquedaDto
        {
            Busqueda = creada.Valor
        });

    var compatibles = await ConsultarAsync(
        token => catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, token), ct);
    if (compatibles.EsFallido)
        return ResultadoOperacion<ResultadoBusquedaDto>.Falla(compatibles.Errores);

    var porCodigo = compatibles.Valor.ToDictionary(
        r => r.CodigoCanonico, StringComparer.OrdinalIgnoreCase);

    var piezas = creada.Valor.CodigosObjetivo
        .Select(codigo => porCodigo.GetValueOrDefault(codigo))
        .OfType<RepuestoDto>()
        .ToList();

    return ResultadoOperacion<ResultadoBusquedaDto>.Exito(new ResultadoBusquedaDto
    {
        Busqueda = creada.Valor,
        Piezas = piezas
    });
}
```

Este flujo consulta el catálogo dos veces. La primera la hace `ObservadorCompatibilidad`, durante
`CrearAsync`, para decidir qué códigos guarda la búsqueda: la entidad persiste solo los códigos
objetivo, no el detalle de cada pieza. La segunda la hace la fachada, y solo si quedaron códigos
asignados, para armar las piezas completas que muestra la pantalla. Es el costo de mantener al
observador desacoplado de la presentación. Con `CatalogoRemoto` son dos llamadas HTTP a
`Catalogo.Api`, y es un punto que se podría optimizar más adelante, por ejemplo con una caché.

Del lado de la presentación, las páginas que necesitan la capa de aplicación inyectan un único punto
de acceso a ella. Las páginas también inyectan servicios propios de la Web, como `SesionActual` y
`TemaActual`, que no forman parte de la capa de aplicación. En `Home.razor`, las directivas
`@inject` del encabezado y, dentro del bloque `@code`, la llamada del método `BuscarAsync`:

```razor
@* src/RepMatch.Web/Components/Pages/Home.razor (fragmento) *@
@inject FachadaAplicacion Fachada
@inject SesionActual Sesion

var respuesta = await Fachada.BuscarAsync(new CrearBusquedaDto
{
    ClienteId = Sesion.Cliente.Id,
    Vehiculo = vehiculo.Datos,
    TextoLibre = texto
});
```

---

## Documentación relacionada

- [Diagrama de componentes](diagramas/componentes.md), dónde vive cada pieza y cómo se referencian
- [Diagrama de clases](diagramas/clases.md), el modelo de dominio sobre el que operan estos patrones
- [Informe de arquitectura](informe-arquitectura.md), decisiones de arquitectura y hoja de ruta
