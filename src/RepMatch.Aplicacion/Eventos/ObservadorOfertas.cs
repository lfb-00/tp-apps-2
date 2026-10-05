using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.Repositorios;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Reacciona a BusquedaCreada consultando los proveedores de ofertas externos (MercadoLibre,
/// eBay, …) en paralelo y registrando los resultados en la búsqueda.
///
/// Corre despues de ObservadorCompatibilidad porque el despachador itera los observadores en
/// orden de registro y este se registra segundo. Al momento en que corre, la busqueda ya tiene
/// CodigosObjetivo asignados y persistidos por el primer observador.
///
/// Si ningun proveedor esta habilitado o todos fallan, la busqueda queda en estado Diagnosticada
/// sin ofertas: la UI muestra las piezas del catalogo interno igualmente.
/// </summary>
public sealed class ObservadorOfertas(
    IBusquedaRepository busquedas,
    IEnumerable<IProveedorOfertas> proveedores,
    IUnitOfWork unidadDeTrabajo,
    ILogger<ObservadorOfertas> log) : IObservadorEventoDominio
{
    public bool PuedeObservar(IEventoDominio evento) => evento is BusquedaCreada;

    public async Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default)
    {
        if (evento is not BusquedaCreada creada)
            return;

        var busqueda = await busquedas.ObtenerPorIdAsync(creada.BusquedaId, ct);
        if (busqueda is null)
        {
            log.LogWarning("ObservadorOfertas: no se encontró la búsqueda {Id}", creada.BusquedaId);
            return;
        }

        // Si el observador de compatibilidad falló no hay códigos que buscar.
        if (busqueda.CodigosObjetivo.Count == 0)
            return;

        var vehiculoDto = creada.Vehiculo.ADto();
        var listaProveedores = proveedores.ToList();

        if (listaProveedores.Count == 0)
        {
            log.LogInformation("ObservadorOfertas: sin proveedores registrados, omitiendo búsqueda de ofertas.");
            return;
        }

        // Llamar a todos los proveedores en paralelo para cada código objetivo.
        var tareas = busqueda.CodigosObjetivo
            .SelectMany(codigo => listaProveedores.Select(p => BuscarConProveedorAsync(p, codigo, vehiculoDto, ct)))
            .ToList();

        var resultados = await Task.WhenAll(tareas);

        var todasLasOfertas = resultados
            .SelectMany(r => r)
            .Where(dto => dto.Precio > 0 && !string.IsNullOrWhiteSpace(dto.Url))
            .Select(dto => new Oferta(
                busqueda.Id,
                dto.CodigoRepuesto,
                dto.NombreTienda,
                dto.Titulo,
                new Dinero(dto.Precio, dto.Moneda),
                dto.Url,
                dto.CostoEnvio.HasValue ? new Dinero(dto.CostoEnvio.Value, dto.Moneda) : null,
                dto.Disponible))
            .ToList();

        busqueda.RegistrarOfertas(todasLasOfertas);
        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation(
            "Búsqueda {Id}: {Total} ofertas registradas ({Proveedores})",
            busqueda.Id,
            todasLasOfertas.Count,
            string.Join(", ", listaProveedores.Select(p => p.Nombre)));
    }

    private async Task<IReadOnlyList<OfertaExternaDto>> BuscarConProveedorAsync(
        IProveedorOfertas proveedor,
        string codigo,
        VehiculoDto vehiculo,
        CancellationToken ct)
    {
        try
        {
            return await proveedor.BuscarAsync(codigo, vehiculo, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Proveedor {Proveedor}: excepción inesperada al buscar '{Codigo}'",
                proveedor.Nombre, codigo);
            return [];
        }
    }
}
