using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// Alguien salio hacia la tienda de una oferta. Es el hecho que mide el ingreso por afiliados; en
/// la Segunda Parte se publica a la cola para contabilizarlo fuera del request.
/// </summary>
public sealed record DerivacionATienda(
    Guid DerivacionId,
    Guid BusquedaId,
    Guid OfertaId,
    string NombreTienda,
    Guid? MecanicoId) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
