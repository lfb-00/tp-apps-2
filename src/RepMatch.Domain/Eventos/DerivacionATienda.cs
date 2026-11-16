using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// Alguien salio hacia la tienda de una oferta. Es el hecho que mide el ingreso por afiliados; en
/// la Segunda Parte se publica a la cola para contabilizarlo fuera del request. Busqueda y oferta
/// son null cuando quien salio no tenia cuenta (<see cref="Entidades.Derivacion.SinCuenta"/>).
/// </summary>
public sealed record DerivacionATienda(
    Guid DerivacionId,
    Guid? BusquedaId,
    Guid? OfertaId,
    string CodigoRepuesto,
    string NombreTienda,
    Guid? MecanicoId) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
