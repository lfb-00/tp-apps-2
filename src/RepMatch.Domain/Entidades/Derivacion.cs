using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Registro de que alguien salio de RepMatch hacia la tienda de una oferta. Es la base del modelo
/// de negocio: la tienda paga una comision de afiliado por las derivaciones que terminan en compra.
/// Es un agregado propio y no parte de <see cref="Busqueda"/> porque tiene que sobrevivir a ella:
/// si el cliente borra su cuenta, la derivacion ya ocurrio y sigue contando para la liquidacion.
/// </summary>
public class Derivacion : EntidadBase
{
    private Derivacion() { }   // requerido por EF Core

    /// <summary>Se crea con <see cref="Busqueda.Derivar"/>, que es quien sabe si la oferta es de
    /// la busqueda y si corresponde atribuirsela a un mecanico.</summary>
    internal Derivacion(Guid busquedaId, Guid ofertaId, string nombreTienda, string urlDestino, Guid? mecanicoId)
    {
        ExcepcionDominio.Si(busquedaId == Guid.Empty, "La derivación debe salir de una búsqueda.");
        ExcepcionDominio.Si(ofertaId == Guid.Empty, "La derivación debe apuntar a una oferta.");
        ExcepcionDominio.SiNulaOVacia(nombreTienda, nameof(nombreTienda));
        ExcepcionDominio.SiNulaOVacia(urlDestino, nameof(urlDestino));

        BusquedaId = busquedaId;
        OfertaId = ofertaId;
        NombreTienda = nombreTienda;
        UrlDestino = urlDestino;
        MecanicoId = mecanicoId;
        OcurridaEn = DateTimeOffset.UtcNow;

        RegistrarEvento(new DerivacionATienda(Id, BusquedaId, OfertaId, NombreTienda, MecanicoId));
    }

    public Guid BusquedaId { get; private set; }
    public Guid OfertaId { get; private set; }
    public string NombreTienda { get; private set; } = string.Empty;
    public string UrlDestino { get; private set; } = string.Empty;

    /// <summary>Mecanico al que se atribuye la derivacion porque recomendo ese repuesto. Sirve
    /// para medir cuanto negocio genera cada profesional.</summary>
    public Guid? MecanicoId { get; private set; }

    public DateTimeOffset OcurridaEn { get; private set; }
}
