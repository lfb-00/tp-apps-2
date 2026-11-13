namespace RepMatch.Contracts.Dtos;

/// <summary>Derivaciones de una tienda en un mes, como las expone <c>GET /api/comisiones</c> del worker.</summary>
public sealed record ComisionesTiendaDto
{
    public required string Tienda { get; init; }

    /// <summary>AAAA-MM, en UTC.</summary>
    public required string Periodo { get; init; }

    public required int Derivaciones { get; init; }
    public required int AtribuidasAMecanico { get; init; }
    public DateTimeOffset? UltimaEn { get; init; }
}
