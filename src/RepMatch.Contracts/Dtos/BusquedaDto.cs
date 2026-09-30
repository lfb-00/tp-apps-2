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
    public IReadOnlyList<OfertaDto> Ofertas { get; init; } = [];
}

public sealed record OfertaDto
{
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
