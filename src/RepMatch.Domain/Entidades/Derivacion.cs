using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Registro de que alguien salio de RepMatch hacia la tienda de una oferta. Es la base del modelo
/// de negocio: la tienda paga una comision de afiliado por las derivaciones que terminan en compra.
/// Es un agregado propio y no parte de <see cref="Busqueda"/> porque tiene que sobrevivir a ella:
/// si el cliente borra su cuenta, la derivacion ya ocurrio y sigue contando para la liquidacion.
///
/// Hay dos clases: la de una busqueda guardada (con oferta, y quiza atribuida a un mecanico) y la
/// de quien compara sin cuenta (<see cref="SinCuenta"/>). Sin cuenta no se guarda la busqueda, asi
/// que no hay busqueda ni oferta a las que apuntar: queda la pieza, la tienda y el destino, nada de
/// la persona. La mayor parte de quienes comparan no tiene cuenta, y sin esta clase su salida a la
/// tienda no contaria para la comision.
/// </summary>
public class Derivacion : EntidadBase
{
    private Derivacion() { }   // requerido por EF Core

    /// <summary>Se crea con <see cref="Busqueda.Derivar"/>, que es quien sabe si la oferta es de
    /// la busqueda y si corresponde atribuirsela a un mecanico.</summary>
    internal Derivacion(
        Guid busquedaId, Guid ofertaId, string codigoRepuesto, string nombreTienda, string urlDestino, Guid? mecanicoId)
    {
        ExcepcionDominio.Si(busquedaId == Guid.Empty, "La derivación debe salir de una búsqueda.");
        ExcepcionDominio.Si(ofertaId == Guid.Empty, "La derivación debe apuntar a una oferta.");

        BusquedaId = busquedaId;
        OfertaId = ofertaId;
        MecanicoId = mecanicoId;
        Completar(codigoRepuesto, nombreTienda, urlDestino);
    }

    /// <summary>
    /// La salida a una tienda de quien compara sin cuenta. Solo acepta destinos http o https: la
    /// URL llega firmada desde la Web, pero la regla es la misma que para <see cref="Oferta"/>.
    /// </summary>
    public static Derivacion SinCuenta(string codigoRepuesto, string nombreTienda, string urlDestino)
    {
        var derivacion = new Derivacion();
        derivacion.Completar(codigoRepuesto, nombreTienda, urlDestino);
        return derivacion;
    }

    private void Completar(string codigoRepuesto, string nombreTienda, string urlDestino)
    {
        ExcepcionDominio.SiNulaOVacia(codigoRepuesto, nameof(codigoRepuesto));
        ExcepcionDominio.SiNulaOVacia(nombreTienda, nameof(nombreTienda));
        ExcepcionDominio.SiNulaOVacia(urlDestino, nameof(urlDestino));
        ExcepcionDominio.Si(!Uri.TryCreate(urlDestino, UriKind.Absolute, out var destino)
                            || (destino.Scheme != Uri.UriSchemeHttp && destino.Scheme != Uri.UriSchemeHttps),
            "La derivación solo puede ir a una dirección http o https.");

        CodigoRepuesto = codigoRepuesto.Trim();
        NombreTienda = nombreTienda.Trim();
        UrlDestino = urlDestino;
        OcurridaEn = DateTimeOffset.UtcNow;

        RegistrarEvento(new DerivacionATienda(Id, BusquedaId, OfertaId, CodigoRepuesto, NombreTienda, MecanicoId));
    }

    /// <summary>null en la derivacion sin cuenta.</summary>
    public Guid? BusquedaId { get; private set; }

    /// <summary>null en la derivacion sin cuenta.</summary>
    public Guid? OfertaId { get; private set; }

    public string CodigoRepuesto { get; private set; } = string.Empty;
    public string NombreTienda { get; private set; } = string.Empty;
    public string UrlDestino { get; private set; } = string.Empty;

    /// <summary>Mecanico al que se atribuye la derivacion porque recomendo ese repuesto. Sirve
    /// para medir cuanto negocio genera cada profesional.</summary>
    public Guid? MecanicoId { get; private set; }

    public DateTimeOffset OcurridaEn { get; private set; }
}
