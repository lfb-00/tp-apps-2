using Microsoft.Extensions.Logging;
using RepMatch.Domain.Eventos;

namespace RepMatch.Aplicacion.Eventos.Manejadores;

/// <summary>
/// Manejador de ejemplo del patron Observer: deja constancia en el log de cada busqueda creada.
///
/// El identificador de correlacion no se pasa a mano: CorrelacionMiddleware lo empuja al
/// LogContext de Serilog, asi que esta linea sale con el mismo [CorrelationId] que el resto de la
/// peticion y se puede cruzar con los logs de Catalogo.Api. El BusquedaId es, ademas, la clave con
/// la que en la Segunda Parte el componente de IA y el agregador de tiendas devuelven sus resultados.
/// </summary>
public sealed class ManejadorLogBusquedaCreada(ILogger<ManejadorLogBusquedaCreada> log)
    : IManejadorEvento<BusquedaCreada>
{
    public Task ManejarAsync(BusquedaCreada evento, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        log.LogInformation(
            "Evento {Evento}: busqueda {BusquedaId} del cliente {ClienteId} para {Vehiculo} " +
            "(\"{TextoLibre}\") ocurrido {OcurridoEn:O}",
            nameof(BusquedaCreada), evento.BusquedaId, evento.ClienteId, evento.Vehiculo,
            evento.TextoLibre, evento.OcurridoEn);

        return Task.CompletedTask;
    }
}
