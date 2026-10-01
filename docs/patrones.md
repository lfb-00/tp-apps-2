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

Hay una segunda razón. La página `/acceso` ejecuta la misma consulta por los dos caminos y compara
las latencias. Para que eso sea posible, la fábrica registra siempre las dos clases concretas y
además enlaza la interfaz a una de ellas. Las dos quedan disponibles, una queda elegida.

En cuanto a la forma, la fábrica no expone un método `Crear` sino que registra un delegado en el
contenedor de dependencias. Es la manera idiomática de aplicar el patrón en .NET, donde la
construcción de objetos ya está delegada en el contenedor. El punto de decisión sigue siendo único
y explícito.

### Qué pasaría sin él

La alternativa que se descartó era que cada consumidor leyera `Catalogo:Modo` y decidiera por su
cuenta. Eso significa repetir el mismo `if` en `Busquedas.razor`, `Catalogo.razor`,
`AccesoComponentes.razor` y `ServicioBusquedas`, es decir cuatro lugares que habría que tocar de
nuevo cada vez que se agregue una forma de acceso. Además la capa de presentación pasaría a depender
de las dos clases concretas en lugar de la interfaz, con lo cual `RepMatch.Web` tendría que
referenciar `RepMatch.Clientes.Rest` incluso corriendo en modo local.

La otra opción era elegir en tiempo de compilación con directivas `#if`, pero obliga a recompilar
para cambiar de modo, y eso anula la posibilidad de configurar el comportamiento por variable de
entorno.

### Código

```csharp
// src/RepMatch.Web/Servicios/FabricaCatalogo.cs
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
`ServicioBusquedas` necesita saber qué repuestos son compatibles con un vehículo, y esa respuesta no
cambia según venga de EF Core o de una llamada HTTP a otro proceso.

Strategy es lo que permite mover esa pieza sin mover nada más. Las dos implementaciones cumplen el
mismo contrato, devuelven los mismos DTOs y son intercambiables. El consumidor no las distingue.

Hay una excepción deliberada en el contrato, que es la propiedad `Modo`. En rigor una estrategia no
debería anunciar cuál es, porque rompe parte de la indistinguibilidad. Está ahí porque el log de
cada operación registra por qué camino se resolvió, y esa información hace falta para diagnosticar
problemas y para comparar el rendimiento de los dos modos. Es una decisión tomada a conciencia.

### Qué pasaría sin él

Sin la interfaz, `ServicioBusquedas` recibiría un `IRepuestoRepository` y un `HttpClient`, y tendría
adentro la rama que decide cuál usar. La lógica de negocio quedaría mezclada con la de transporte, y
cada prueba de ese servicio tendría que montar las dos dependencias aunque solo ejercite una.

El costo se ve mejor pensando en una tercera forma de acceso. Con Strategy, agregarla es escribir una
clase nueva y sumar una rama en la fábrica. Sin Strategy, hay que tocar cada consumidor que tenga la
decisión duplicada.

### Código

```csharp
// src/RepMatch.Contracts/ICatalogoRepuestos.cs
public interface ICatalogoRepuestos
{
    string Modo { get; }   // "Local" | "Remoto". Queda en el log de cada operacion

    Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default);

    Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default);
    Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default);
}
```

El consumidor ve una sola cosa, que es la interfaz:

```csharp
// src/RepMatch.Aplicacion/Servicios/ServicioBusquedas.cs
public sealed class ServicioBusquedas(
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
    ICatalogoRepuestos catalogo,        // la estrategia, sin saber cual es
    IUnitOfWork unidadDeTrabajo, ...)
{
    // ...
    var compatibles = await catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, ct);
}
```

| | Local | Remoto |
|---|---|---|
| Implementación | `Aplicacion.Catalogo.CatalogoLocal` | `Clientes.Rest.CatalogoRemoto` |
| Mecanismo | invocación directa en proceso | HTTP + JSON contra `Catalogo.Api` |
| Serialización | ninguna | JSON de ida y vuelta |
| Latencia medida en caliente | ~1,9 ms | ~12,6 ms |

---

## Repository: `I*Repository` con implementaciones en Persistence

### Dónde está

Las interfaces en `src/RepMatch.Domain/Repositorios/`, que son `IClienteRepository`,
`IRepuestoRepository` e `IBusquedaRepository`. Las implementaciones en
`src/RepMatch.Persistence/Repositorios/`.

### Qué problema resuelve

El dominio tiene reglas que conviene probar por separado: la compatibilidad de un repuesto con un
vehículo en `Repuesto.EsCompatibleCon`, las transiciones de estado de una `Busqueda` y la
comparación de precios de `Oferta` dentro de una misma moneda. Si esas entidades dependieran de EF
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
}
```

La implementación, en persistencia, donde sí vive EF y el reparto entre SQL y dominio:

```csharp
// src/RepMatch.Persistence/Repositorios/RepuestoRepository.cs
var consulta = contexto.Repuestos
    .Include(r => r.Aplicaciones)
    .Where(r => r.Aplicaciones.Any(a =>
        a.Marca.ToLower() == vehiculo.Marca.ToLower()
        && a.Modelo.ToLower() == vehiculo.Modelo.ToLower()
        && a.AnioDesde <= vehiculo.Anio
        && a.AnioHasta >= vehiculo.Anio));

var candidatos = await consulta.OrderBy(r => r.CodigoCanonico).ToListAsync(ct);

// El filtro grueso lo hace la base; la regla fina del motor la hace la entidad.
return [.. candidatos.Where(r => r.EsCompatibleCon(vehiculo))];
```

---

## Unit of Work: `IUnitOfWork` y `UnitOfWork`

### Dónde está

`src/RepMatch.Domain/Repositorios/IUnitOfWork.cs` y `src/RepMatch.Persistence/UnitOfWork.cs`.

### Qué problema resuelve

Crear una búsqueda no es una sola escritura. Se agrega la `Busqueda`, se le asignan los códigos
objetivo que devolvió el catálogo y, cuando entren las ofertas, se agregan las filas hijas. Todo eso
tiene que confirmarse junto. Una búsqueda guardada sin sus códigos objetivo queda en estado
`Diagnosticada` pero sin nada para buscar, que es un registro inconsistente.

`DbContext` ya funciona como unidad de trabajo, porque rastrea los cambios y los confirma en una
transacción. La interfaz existe para que `ServicioBusquedas` pueda pedir la confirmación sin conocer
EF Core. Si el servicio llamara directamente a `contexto.SaveChangesAsync`, `RepMatch.Aplicacion`
tendría que referenciar EF y la inversión de dependencias se rompería por ahí.

La implementación es un envoltorio de una línea. No está por la lógica que agrega sino por el
acoplamiento que evita.

### Qué pasaría sin él

La alternativa descartada era que cada repositorio guardara por su cuenta, con un `SaveChanges`
dentro de `AgregarAsync`. Eso rompe la atomicidad: si la escritura de los códigos objetivo falla
después de que la búsqueda ya se guardó, queda una fila inconsistente que nadie limpia. Además
multiplica los viajes a la base, porque una operación de negocio pasa a ser varias transacciones.

La otra alternativa era inyectar el `DbContext` en la capa de aplicación. Resuelve la atomicidad
pero deja el acoplamiento que se describe arriba.

### Código

```csharp
// src/RepMatch.Domain/Repositorios/IUnitOfWork.cs, declarado en el dominio
public interface IUnitOfWork
{
    Task<int> ConfirmarAsync(CancellationToken ct = default);
}
```

```csharp
// src/RepMatch.Persistence/UnitOfWork.cs, implementado sobre EF Core
public sealed class UnitOfWork(RepMatchDbContext contexto) : IUnitOfWork
{
    public Task<int> ConfirmarAsync(CancellationToken ct = default) =>
        contexto.SaveChangesAsync(ct);
}
```

Los repositorios acumulan los cambios y el servicio confirma una sola vez, al final de la operación:

```csharp
// src/RepMatch.Aplicacion/Servicios/ServicioBusquedas.cs
var busqueda = new Busqueda(dto.ClienteId, dto.Vehiculo.AEntidad(), dto.TextoLibre);
var compatibles = await catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, ct);

if (compatibles.Count > 0)
    busqueda.AsignarCodigosObjetivo(compatibles.Select(r => r.CodigoCanonico));
else
    busqueda.Fallar($"Todavia no tenemos repuestos catalogados para un {dto.Vehiculo}.");

await busquedas.AgregarAsync(busqueda, ct);
await unidadDeTrabajo.ConfirmarAsync(ct);   // una sola transaccion
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

Sin el adaptador, el `HttpClient` se inyectaría en los consumidores y `Busquedas.razor` tendría que
armar la query string, revisar el `StatusCode`, deserializar y decidir qué hacer con un 404, y eso se
repetiría en cada página que consulte el catálogo. Además el modo local dejaría de ser intercambiable
con el remoto, porque los consumidores ya estarían escritos contra HTTP.

Queda una filtración conocida y acotada. `Busquedas.razor` atrapa `HttpRequestException` y
`TaskCanceledException` para mostrar un mensaje entendible cuando la API remota no responde. El
adaptador traduce los errores de protocolo pero deja pasar los de transporte. Sin adaptador, la
filtración no sería una excepción puntual sino todo el modelo HTTP.

### Código

```csharp
// src/RepMatch.Clientes.Rest/CatalogoRemoto.cs
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
            return null;                       // 404 (HTTP) se traduce a null (contrato del dominio)

        respuesta.EnsureSuccessStatusCode();   // el resto de los errores, como excepcion

        return await respuesta.Content.ReadFromJsonAsync<RepuestoDto>(ct);
    }
}
```

Del otro lado, `CatalogoLocal` cumple el mismo contrato sin traducir nada, porque no hay ningún
protocolo ajeno del que defenderse:

```csharp
// src/RepMatch.Aplicacion/Catalogo/CatalogoLocal.cs
public async Task<RepuestoDto?> ObtenerPorCodigoAsync(
    string codigoCanonico, CancellationToken ct = default)
{
    var repuesto = await repuestos.ObtenerPorCodigoAsync(codigoCanonico, ct);
    return repuesto?.ADto();
}
```

La asimetría entre las dos implementaciones es el patrón Adapter: todo lo que `CatalogoRemoto` hace
de más es traducción.

---

## Documentación relacionada

- [Diagrama de componentes](diagramas/componentes.md), dónde vive cada pieza y cómo se referencian
- [Diagrama de clases](diagramas/clases.md), el modelo de dominio sobre el que operan estos patrones
- [Informe de arquitectura](informe-arquitectura.md), decisiones de arquitectura y hoja de ruta
