using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Observer de un hecho del dominio. Cada implementación decide qué eventos le importan.
/// </summary>
public interface IObservadorEventoDominio
{
    bool PuedeObservar(IEventoDominio evento);

    Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default);
}
