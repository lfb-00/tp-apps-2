using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// El cliente pidio ayuda con un problema. En la Segunda Parte este mismo evento se publica en
/// RabbitMQ y lo consumen el componente de IA (para diagnosticar) y el agregador (para consultar
/// las tiendas). En el TP Inicial solo se acumula en la entidad.
/// </summary>
public sealed record BusquedaCreada(
    Guid BusquedaId,
    Guid ClienteId,
    DatosVehiculo Vehiculo,
    string TextoLibre) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
