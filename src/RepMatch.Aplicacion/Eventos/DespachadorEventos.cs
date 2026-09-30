using Microsoft.Extensions.Logging;
using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

public sealed class DespachadorEventos(
    IEnumerable<IObservadorEventoDominio> observadores,
    ILogger<DespachadorEventos> log) : IDespachadorEventos
{
    public async Task DespacharAsync(IEnumerable<IEventoDominio> eventos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        foreach (var evento in eventos)
        {
            foreach (var observador in observadores)
            {
                if (!observador.PuedeObservar(evento))
                    continue;

                log.LogInformation(
                    "Evento {Evento} observado por {Observador}",
                    evento.GetType().Name, observador.GetType().Name);

                await observador.ObservarAsync(evento, ct);
            }
        }
    }
}
