namespace RepMatch.Contracts.Dtos;

/// <summary>Un aviso listo para mostrar. El texto se arma al leerlo: la base guarda solo ids.</summary>
public sealed record NotificacionDto
{
    public required Guid Id { get; init; }
    public required Guid BusquedaId { get; init; }
    public required string Titulo { get; init; }
    public required string Detalle { get; init; }
    public required DateTimeOffset CreadaEn { get; init; }
    public required bool Leida { get; init; }
}

/// <summary>Lo que muestra la campana del encabezado.</summary>
public sealed record BandejaNotificacionesDto
{
    public required int NoLeidas { get; init; }
    public IReadOnlyList<NotificacionDto> Recientes { get; init; } = [];
}
