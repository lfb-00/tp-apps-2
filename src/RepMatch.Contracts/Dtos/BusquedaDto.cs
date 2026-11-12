using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

public sealed record BusquedaDto
{
    public required Guid Id { get; init; }
    public required Guid ClienteId { get; init; }
    public required VehiculoDto Vehiculo { get; init; }
    public required string TextoLibre { get; init; }
    public required string Estado { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
    public string? MotivoFalla { get; init; }
    public IReadOnlyList<string> CodigosObjetivo { get; init; } = [];

    /// <summary>null si la búsqueda no pasó por la IA: los códigos salieron solo por compatibilidad.</summary>
    public DiagnosticoBusquedaDto? Diagnostico { get; init; }

    public IReadOnlyList<OfertaDto> Ofertas { get; init; } = [];

    /// <summary>null mientras no se consultaron precios. Con esto la pantalla dice de cuando son.</summary>
    public DateTimeOffset? PreciosConsultadosEn { get; init; }

    public Guid? MecanicoId { get; init; }
    public DateTimeOffset? CompartidaEn { get; init; }

    /// <summary>null mientras el mecanico no la reviso.</summary>
    public RevisionDto? Revision { get; init; }
}

public sealed record OfertaDto
{
    /// <summary>Lo usa el enlace de derivacion (/derivar/{id}) para registrar la salida a la tienda.</summary>
    public Guid Id { get; init; }

    public required string CodigoRepuesto { get; init; }
    public required string NombreTienda { get; init; }
    public required string Titulo { get; init; }
    public required decimal Precio { get; init; }
    public required string Moneda { get; init; }
    public decimal? CostoEnvio { get; init; }
    public required decimal PrecioTotal { get; init; }
    public required string UrlOriginal { get; init; }
    public required bool Disponible { get; init; }
    public required DateTimeOffset CapturadaEn { get; init; }

    /// <summary>Precio antes del descuento. null si la tienda no muestra descuento.</summary>
    public decimal? PrecioLista { get; init; }

    /// <summary>Ver <see cref="CoincidenciasOferta"/>.</summary>
    public string Coincidencia { get; init; } = CoincidenciasOferta.ProbablePorNombre;

    public string? Marca { get; init; }
    public string? Ean { get; init; }
    public string? Vendedor { get; init; }
}

/// <summary>
/// Respuesta de pedir los precios de una busqueda. Las tiendas que no respondieron van aparte para
/// que la pantalla avise sin esconder lo que si llego.
/// </summary>
public sealed record OfertasBusquedaDto
{
    public required BusquedaDto Busqueda { get; init; }
    public IReadOnlyList<string> TiendasSinRespuesta { get; init; } = [];
}

/// <summary>
/// Lo que ve quien busca sin cuenta: desde cuanto y en cuantas tiendas, pero no donde. Sin
/// busqueda guardada no hay oferta guardada, y sin oferta no hay derivacion que registrar.
/// </summary>
public sealed record ResumenPreciosDto
{
    public required string CodigoRepuesto { get; init; }

    /// <summary>null si ninguna tienda la publica.</summary>
    public decimal? Desde { get; init; }

    public string Moneda { get; init; } = "ARS";
    public int Tiendas { get; init; }
}

public sealed record ResumenPreciosBusquedaDto
{
    public IReadOnlyList<ResumenPreciosDto> Piezas { get; init; } = [];
    public IReadOnlyList<string> TiendasSinRespuesta { get; init; } = [];
}

public sealed record CrearBusquedaDto
{
    [Required]
    public required Guid ClienteId { get; init; }

    [Required]
    public required VehiculoDto Vehiculo { get; init; }

    /// <summary>Descripción del problema en las palabras del cliente. Se guarda con la búsqueda.</summary>
    [Required, StringLength(1000, MinimumLength = 5)]
    public required string TextoLibre { get; init; }
}
