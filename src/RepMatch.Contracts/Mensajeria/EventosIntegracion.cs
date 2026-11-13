namespace RepMatch.Contracts.Mensajeria;

// Datos de cada evento de integracion, version 1. Llevan ids y lo minimo para actuar, nunca el
// texto libre del problema ni datos personales: un mensaje puede quedar guardado en la cola, en la
// DLQ o en un log, y lo que no viaja no se filtra.

/// <summary>Se guardo una busqueda con su diagnostico.</summary>
public sealed record BusquedaCreadaV1
{
    public required Guid BusquedaId { get; init; }
    public required Guid ClienteId { get; init; }
    public required string Marca { get; init; }
    public required string Modelo { get; init; }
    public required int Anio { get; init; }
    public string? Motor { get; init; }
}

/// <summary>El cliente le compartio una consulta a un mecanico.</summary>
public sealed record BusquedaCompartidaV1
{
    public required Guid BusquedaId { get; init; }
    public required Guid ClienteId { get; init; }
    public required Guid MecanicoId { get; init; }
}

/// <summary>El mecanico confirmo o corrigio el diagnostico. Propuestos y recomendados son el dato
/// etiquetado con que se mide la exactitud de la IA.</summary>
public sealed record DiagnosticoValidadoV1
{
    public required Guid BusquedaId { get; init; }
    public required Guid ClienteId { get; init; }
    public required Guid MecanicoId { get; init; }
    public required bool Confirmado { get; init; }
    public IReadOnlyList<string> CodigosPropuestos { get; init; } = [];
    public IReadOnlyList<string> CodigosRecomendados { get; init; } = [];
}

/// <summary>Alguien salio de RepMatch hacia una tienda: la base de la comision de afiliado.</summary>
public sealed record DerivacionTiendaV1
{
    public required Guid DerivacionId { get; init; }
    public required Guid BusquedaId { get; init; }
    public required Guid OfertaId { get; init; }
    public required string Tienda { get; init; }
    public Guid? MecanicoId { get; init; }
}
