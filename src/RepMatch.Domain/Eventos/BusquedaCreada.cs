using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// El cliente pidio ayuda con un problema. Hoy lo despacha en proceso la unidad de trabajo al
/// confirmar; en la Segunda Parte este mismo evento se publica en RabbitMQ y lo consumen el
/// componente de IA (para diagnosticar) y el agregador (para consultar las tiendas).
/// </summary>
public sealed record BusquedaCreada(
    Guid BusquedaId,
    Guid ClienteId,
    DatosVehiculo Vehiculo,
    string TextoLibre) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
