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
        bool disponible = true)
    {
        ExcepcionDominio.Si(busquedaId == Guid.Empty, "La oferta debe pertenecer a una busqueda.");
        ExcepcionDominio.SiNulaOVacia(codigoRepuesto, nameof(codigoRepuesto));
        ExcepcionDominio.SiNulaOVacia(nombreTienda, nameof(nombreTienda));
        ExcepcionDominio.SiNulaOVacia(titulo, nameof(titulo));
        ExcepcionDominio.SiNulaOVacia(urlOriginal, nameof(urlOriginal));
        ArgumentNullException.ThrowIfNull(precio);
        ExcepcionDominio.Si(!Uri.TryCreate(urlOriginal, UriKind.Absolute, out _),
            $"La URL de la oferta no es absoluta: '{urlOriginal}'.");

        BusquedaId = busquedaId;
        CodigoRepuesto = codigoRepuesto.Trim().ToUpperInvariant();
        NombreTienda = nombreTienda.Trim();
        Titulo = titulo.Trim();
        Precio = precio;
        CostoEnvio = costoEnvio;
        UrlOriginal = urlOriginal.Trim();
        Disponible = disponible;
        CapturadaEn = DateTimeOffset.UtcNow;
    }

    public Guid BusquedaId { get; private set; }
    public string CodigoRepuesto { get; private set; } = string.Empty;
    public string NombreTienda { get; private set; } = string.Empty;
    public string Titulo { get; private set; } = string.Empty;
    public Dinero Precio { get; private set; }
    public Dinero? CostoEnvio { get; private set; }
    public string UrlOriginal { get; private set; } = string.Empty;
    public bool Disponible { get; private set; }

    /// <summary>Momento de la captura: los precios de las tiendas externas envejecen rapido y la
    /// UI necesita poder advertirlo.</summary>
    public DateTimeOffset CapturadaEn { get; private set; }

    /// <summary>Precio final comparable entre tiendas: sin esto, una oferta barata con envio caro
    /// ganaria el ranking indebidamente.</summary>
    public Dinero PrecioTotal => CostoEnvio is null ? Precio : Precio + CostoEnvio;

    public bool EstaVencida(TimeSpan antiguedadMaxima) =>
        DateTimeOffset.UtcNow - CapturadaEn > antiguedadMaxima;
}
