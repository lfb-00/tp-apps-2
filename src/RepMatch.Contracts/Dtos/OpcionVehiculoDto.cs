namespace RepMatch.Contracts.Dtos;

/// <summary>Una configuración permitida en el catálogo de vehículos.</summary>
public sealed record OpcionVehiculoDto
{
    public required string Marca { get; init; }
    public required string Modelo { get; init; }
    public required int AnioDesde { get; init; }
    public required int AnioHasta { get; init; }
    public required string Motor { get; init; }
}
