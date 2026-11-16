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
/// Los precios de quien compara sin cuenta: las mismas ofertas que ve el cliente, sin guardar
/// nada. Como no hay oferta guardada, su <see cref="OfertaDto.Id"/> es vacio y la salida a la
/// tienda no pasa por /derivar sino por un enlace firmado que registra una derivacion sin cuenta.
/// </summary>
public sealed record PreciosSinCuentaDto
{
    public IReadOnlyList<OfertaDto> Ofertas { get; init; } = [];
    public IReadOnlyList<string> TiendasSinRespuesta { get; init; } = [];
}

/// <summary>Una tienda de repuestos donde buscar una pieza que no tiene ofertas exactas. No es
/// una oferta: es un enlace a la busqueda del propio sitio, sin precio.</summary>
public sealed record SugerenciaTiendaDto
{
    public required string Tienda { get; init; }
    public required string Url { get; init; }
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
