namespace RepMatch.Contracts.Dtos;

public sealed record AplicacionDto
{
    public required string Marca { get; init; }
    public required string Modelo { get; init; }
    public required int AnioDesde { get; init; }
    public required int AnioHasta { get; init; }
    public string? Motor { get; init; }
}

/// <summary>
/// Repuesto del catalogo tal como lo devuelven las dos implementaciones de
/// <see cref="ICatalogoRepuestos"/>. Que el DTO sea el mismo por los dos caminos es justamente
/// lo que hace demostrable que local y remoto son intercambiables.
/// </summary>
public sealed record RepuestoDto
{
    public required Guid Id { get; init; }
    public required string CodigoCanonico { get; init; }
    public required string Nombre { get; init; }
    public string? Descripcion { get; init; }
    public required string Sistema { get; init; }
    public IReadOnlyList<string> CodigosEquivalentes { get; init; } = [];
    public IReadOnlyList<AplicacionDto> Aplicaciones { get; init; } = [];
}
