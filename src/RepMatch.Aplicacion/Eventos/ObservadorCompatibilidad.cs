using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Contracts;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Reacciona a <see cref="BusquedaCreada"/> consultando el catálogo. El servicio que crea la
/// búsqueda no conoce este paso: se entera del hecho por el evento.
/// </summary>
public sealed class ObservadorCompatibilidad(
    IBusquedaRepository busquedas,
    ICatalogoRepuestos catalogo,
    IUnitOfWork unidadDeTrabajo,
    ILogger<ObservadorCompatibilidad> log) : IObservadorEventoDominio
{
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
}
