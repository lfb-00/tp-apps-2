using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Una publicacion concreta encontrada en una tienda externa: el mismo repuesto, con precio,
/// envio y URL propios. RepMatch nunca vende esta oferta ni reserva stock: solo la normaliza,
/// la compara y deriva al sitio de origen.
/// </summary>
public class Oferta : EntidadBase
{
    private Oferta() { Precio = null!; }   // requerido por EF Core

    public Oferta(
        Guid busquedaId,
        string codigoRepuesto,
        string nombreTienda,
        string titulo,
        Dinero precio,
        string urlOriginal,
        Dinero? costoEnvio = null,
        bool disponible = true,
        Dinero? precioLista = null,
        CoincidenciaOferta coincidencia = CoincidenciaOferta.ProbablePorNombre,
        string? marca = null,
        string? ean = null,
        string? referenciaTienda = null,
        string? vendedor = null,
        DateTimeOffset? capturadaEn = null)
    {
        ExcepcionDominio.Si(busquedaId == Guid.Empty, "La oferta debe pertenecer a una busqueda.");
        ExcepcionDominio.SiNulaOVacia(codigoRepuesto, nameof(codigoRepuesto));
        ExcepcionDominio.SiNulaOVacia(nombreTienda, nameof(nombreTienda));
        ExcepcionDominio.SiNulaOVacia(titulo, nameof(titulo));
        ExcepcionDominio.SiNulaOVacia(urlOriginal, nameof(urlOriginal));
        ArgumentNullException.ThrowIfNull(precio);
        // Solo http o https: la URL viene de una tienda externa y termina en un enlace de la pagina
        // (directo para el mecanico) o en la redireccion de /derivar. Una URL "javascript:" tambien
        // es absoluta y se ejecutaria dentro de RepMatch al hacer clic.
        ExcepcionDominio.Si(!Uri.TryCreate(urlOriginal, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps),
            $"La URL de la oferta tiene que ser http o https: '{urlOriginal}'.");

        BusquedaId = busquedaId;
        CodigoRepuesto = codigoRepuesto.Trim().ToUpperInvariant();
        NombreTienda = nombreTienda.Trim();
        Titulo = titulo.Trim();
        Precio = precio;
        CostoEnvio = costoEnvio;
        UrlOriginal = urlOriginal.Trim();
        Disponible = disponible;
        PrecioLista = DescuentoReal(precio, precioLista);
        Coincidencia = coincidencia;
        Marca = Limpio(marca);
        Ean = Limpio(ean);
        ReferenciaTienda = Limpio(referenciaTienda);
        Vendedor = Limpio(vendedor);
        CapturadaEn = capturadaEn ?? DateTimeOffset.UtcNow;
    }

    public Guid BusquedaId { get; private set; }
    public string CodigoRepuesto { get; private set; } = string.Empty;
    public string NombreTienda { get; private set; } = string.Empty;
    public string Titulo { get; private set; } = string.Empty;
    public Dinero Precio { get; private set; }
    public Dinero? CostoEnvio { get; private set; }
    public string UrlOriginal { get; private set; } = string.Empty;
    public bool Disponible { get; private set; }

    /// <summary>Precio antes del descuento que muestra la tienda. null si no hay descuento.</summary>
    public Dinero? PrecioLista { get; private set; }

    public CoincidenciaOferta Coincidencia { get; private set; }
    public string? Marca { get; private set; }

    /// <summary>Codigo de barras usado para reconocer la misma pieza entre tiendas (issue #29).</summary>
    public string? Ean { get; private set; }

    /// <summary>Id de la publicacion en la tienda: distingue dos publicaciones de la misma pieza.</summary>
    public string? ReferenciaTienda { get; private set; }

    /// <summary>En un marketplace, quien vende de verdad.</summary>
    public string? Vendedor { get; private set; }

    /// <summary>Momento de la captura: los precios de las tiendas externas envejecen rapido y la
    /// UI necesita poder advertirlo.</summary>
    public DateTimeOffset CapturadaEn { get; private set; }

    /// <summary>Precio final comparable entre tiendas: sin esto, una oferta barata con envio caro
    /// ganaria el ranking indebidamente.</summary>
    public Dinero PrecioTotal => CostoEnvio is null ? Precio : Precio + CostoEnvio;

    public bool EstaVencida(TimeSpan antiguedadMaxima) =>
        DateTimeOffset.UtcNow - CapturadaEn > antiguedadMaxima;

    /// <summary>
    /// Misma publicacion de la misma tienda para la misma pieza. Es el criterio de deduplicacion:
    /// la referencia de la tienda si la hay, si no el EAN, si no la URL.
    /// </summary>
    public bool EsLaMismaPublicacionQue(Oferta otra)
    {
        ArgumentNullException.ThrowIfNull(otra);

        if (CodigoRepuesto != otra.CodigoRepuesto
            || !string.Equals(NombreTienda, otra.NombreTienda, StringComparison.OrdinalIgnoreCase))
            return false;

        if (ReferenciaTienda is not null && otra.ReferenciaTienda is not null)
            return ReferenciaTienda == otra.ReferenciaTienda;

        if (Ean is not null && otra.Ean is not null)
            return Ean == otra.Ean;

        return string.Equals(UrlOriginal, otra.UrlOriginal, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pisa precio y datos con una captura nueva de la misma publicacion. Se actualiza en lugar de
    /// reemplazarla porque una <see cref="Derivacion"/> puede apuntar a esta oferta por su Id: la
    /// comision tiene que seguir sabiendo a que publicacion se derivo.
    /// </summary>
    public void ActualizarCon(Oferta captura)
    {
        ArgumentNullException.ThrowIfNull(captura);
        ExcepcionDominio.Si(!EsLaMismaPublicacionQue(captura), "La captura es de otra publicación.");

        Titulo = captura.Titulo;
        Precio = captura.Precio;
        CostoEnvio = captura.CostoEnvio;
        PrecioLista = captura.PrecioLista;
        UrlOriginal = captura.UrlOriginal;
        Disponible = captura.Disponible;
        Coincidencia = captura.Coincidencia > Coincidencia ? captura.Coincidencia : Coincidencia;
        Marca = captura.Marca ?? Marca;
        Ean = captura.Ean ?? Ean;
        Vendedor = captura.Vendedor;
        CapturadaEn = captura.CapturadaEn;
    }

    /// <summary>La tienda ya no la publica (o no tiene stock): queda en el historial, fuera del ranking.</summary>
    public void MarcarNoDisponible(DateTimeOffset consultadaEn)
    {
        Disponible = false;
        CapturadaEn = consultadaEn;
    }

    private static Dinero? DescuentoReal(Dinero precio, Dinero? precioLista) =>
        precioLista is not null && precioLista.Moneda == precio.Moneda && precio.EsMasBaratoQue(precioLista)
            ? precioLista
            : null;

    private static string? Limpio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
