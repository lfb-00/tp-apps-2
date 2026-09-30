using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// El cliente registró una búsqueda. Lo observa el asignador de compatibilidad, que completa
/// los códigos de pieza sin que el servicio de búsquedas conozca el catálogo.
/// </summary>
public sealed record BusquedaCreada(
    Guid BusquedaId,
    Guid ClienteId,
    DatosVehiculo Vehiculo,
    string TextoLibre) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
