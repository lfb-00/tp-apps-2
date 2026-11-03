using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// El cliente le compartio una busqueda a un mecanico. Hoy no tiene observadores; en la Segunda
/// Parte el worker de notificaciones lo consume de la cola para avisarle al mecanico.
/// </summary>
public sealed record BusquedaCompartida(
    Guid BusquedaId,
    Guid ClienteId,
    Guid MecanicoId) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
