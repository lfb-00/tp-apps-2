using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Reacciona a <see cref="BusquedaCreada"/> consultando el catálogo. El servicio que crea la
/// búsqueda no conoce este paso: se entera del hecho por el evento.
///
/// En la Segunda Parte la IA se sumó acá, sin tocar <c>ServicioBusquedas.CrearAsync</c>: la
/// compatibilidad sigue siendo el filtro y <see cref="ServicioDiagnostico"/> elige entre esas
/// piezas leyendo el texto libre. Si la IA no responde, los códigos salen por compatibilidad
/// como en la Primera Parte.
/// </summary>
public sealed class ObservadorCompatibilidad(
    IBusquedaRepository busquedas,
    ServicioDiagnostico diagnostico,
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
            var piezas = await diagnostico.DiagnosticarAsync(creada.Vehiculo.ADto(), creada.TextoLibre, ct);

            if (piezas.Diagnostico is { } d)
                busqueda.RegistrarDiagnostico(
                    Enum.TryParse<SistemaVehiculo>(d.Sistema, ignoreCase: true, out var sistema) ? sistema : SistemaVehiculo.Desconocido,
                    Enum.TryParse<NivelUrgencia>(d.Urgencia, ignoreCase: true, out var urgencia) ? urgencia : NivelUrgencia.Desconocida,
                    d.Causas.Select(c => c.Causa),
                    d.Fuente);

            if (piezas.Elegidas.Count > 0)
                busqueda.AsignarCodigosObjetivo(piezas.Elegidas.Select(r => r.CodigoCanonico));
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
            "Búsqueda {BusquedaId} completada: estado {Estado}, {Codigos} códigos, diagnóstico {Fuente}",
            busqueda.Id, busqueda.Estado, busqueda.CodigosObjetivo.Count, busqueda.FuenteDiagnostico ?? "sin IA");
    }
}
