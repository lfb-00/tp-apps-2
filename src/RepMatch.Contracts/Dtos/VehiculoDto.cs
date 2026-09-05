using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

/// <summary>Vehiculo tal como viaja por la frontera del componente (HTTP o llamada directa).</summary>
public sealed record VehiculoDto
{
    [Required, StringLength(50, MinimumLength = 1)]
    public required string Marca { get; init; }

    [Required, StringLength(50, MinimumLength = 1)]
    public required string Modelo { get; init; }

    [Range(1950, 2100)]
    public required int Anio { get; init; }

    [StringLength(50)]
    public string? Motor { get; init; }

    public override string ToString() =>
        Motor is null ? $"{Marca} {Modelo} {Anio}" : $"{Marca} {Modelo} {Anio} ({Motor})";
}
