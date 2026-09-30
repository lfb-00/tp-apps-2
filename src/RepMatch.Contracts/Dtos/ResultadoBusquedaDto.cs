namespace RepMatch.Contracts.Dtos;

/// <summary>
/// Una búsqueda ya guardada y las piezas del catálogo que le corresponden.
/// </summary>
public sealed record ResultadoBusquedaDto
{
    public required BusquedaDto Busqueda { get; init; }
    public IReadOnlyList<RepuestoDto> Piezas { get; init; } = [];
}
