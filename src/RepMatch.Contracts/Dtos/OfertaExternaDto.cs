namespace RepMatch.Contracts.Dtos;

/// <summary>Como llego una oferta a una pieza del catalogo. Va en texto para que viaje igual por
/// REST, SOAP o la cola.</summary>
public static class CoincidenciasOferta
{
    /// <summary>La publicacion trae el EAN consultado o comparte EAN con otra tienda.</summary>
    public const string ExactoPorEan = "ExactoPorEan";

    /// <summary>La publicacion menciona un codigo OEM o equivalente de la pieza.</summary>
    public const string ExactoPorCodigo = "ExactoPorCodigo";

    /// <summary>La publicacion coincide por nombre y no se pudo confirmar con una identidad exacta.</summary>
    public const string ProbablePorNombre = "ProbablePorNombre";
}

/// <summary>Lo que se le pregunta a cada tienda: una pieza del catalogo y como reconocerla.</summary>
public sealed record ConsultaOfertasDto
{
    public required string CodigoRepuesto { get; init; }
    public required string Nombre { get; init; }

    /// <summary>Codigos OEM y de fabricantes alternativos. Son los terminos mas precisos.</summary>
    public IReadOnlyList<string> CodigosEquivalentes { get; init; } = [];
}

/// <summary>
/// Una publicacion de una tienda, normalizada por el adapter. Si dos tiendas publican el mismo
/// EAN, ambas ofertas se clasifican como coincidencias exactas de la misma pieza (issue #29).
/// </summary>
public sealed record OfertaExternaDto
{
    public required string CodigoRepuesto { get; init; }
    public required string Tienda { get; init; }
    public required string Titulo { get; init; }
    public required decimal Precio { get; init; }

    /// <summary>Precio sin descuento, solo si es mayor que <see cref="Precio"/>.</summary>
    public decimal? PrecioLista { get; init; }

    /// <summary>ISO-4217. Lo fija la configuracion de la tienda: VTEX no informa la moneda.</summary>
    public required string Moneda { get; init; }

    public required string Url { get; init; }
    public required string Coincidencia { get; init; }
    public string? Marca { get; init; }
    public string? Ean { get; init; }
    public IReadOnlyList<string> Atributos { get; init; } = [];
    public DateTimeOffset CapturadaEn { get; init; }

    /// <summary>Identificador de la publicacion dentro de la tienda. Junto con la tienda, distingue
    /// dos publicaciones distintas de la misma pieza.</summary>
    public string? Referencia { get; init; }

    /// <summary>En los marketplaces (Fravega) quien vende de verdad: no siempre es la tienda.</summary>
    public string? Vendedor { get; init; }
}
