using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

/// <summary>
/// Lo que recibe el componente de IA. Los candidatos son las piezas del catalogo compatibles con
/// el vehiculo: el diagnostico elige entre ellas en vez de proponer codigos libres, asi la
/// compatibilidad la sigue resolviendo el catalogo y no el modelo.
/// </summary>
public sealed record SolicitudDiagnosticoDto
{
    [Required, StringLength(500, MinimumLength = 5)]
    public required string Texto { get; init; }

    [Required]
    public required VehiculoDto Vehiculo { get; init; }

    public IReadOnlyList<CandidatoDiagnosticoDto> Candidatos { get; init; } = [];
}

public sealed record CandidatoDiagnosticoDto
{
    [Required, StringLength(60, MinimumLength = 1)]
    public required string Codigo { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Nombre { get; init; }

    [Required, StringLength(30, MinimumLength = 1)]
    public required string Sistema { get; init; }
}

/// <summary>
/// Respuesta del componente de IA. <see cref="Fuente"/> y <see cref="LatenciaMs"/> van siempre:
/// son las metricas que el Integrador pide medir (quien respondio y cuanto tardo).
/// </summary>
public sealed record DiagnosticoDto
{
    /// <summary>Nombre de <c>SistemaVehiculo</c> ("Frenos", "Motor", ...) o "Desconocido".</summary>
    public required string Sistema { get; init; }

    /// <summary>Nombre de <c>NivelUrgencia</c> ("Baja", "Media", "Alta", "Critica") o "Desconocida".</summary>
    public required string Urgencia { get; init; }

    public IReadOnlyList<CausaProbableDto> Causas { get; init; } = [];

    /// <summary>Codigos canonicos elegidos entre los candidatos, del mas al menos probable.</summary>
    public IReadOnlyList<string> Codigos { get; init; } = [];

    /// <summary>"llm" o "fallback-local".</summary>
    public required string Fuente { get; init; }

    public required long LatenciaMs { get; init; }
}

public sealed record CausaProbableDto
{
    public required string Causa { get; init; }

    /// <summary>Entre 0 y 1.</summary>
    public required double Confianza { get; init; }
}

/// <summary>
/// Resultado de la busqueda sin sesion: diagnostico y piezas, sin guardar nada. La persona no
/// tiene cuenta, asi que su texto no se persiste.
/// </summary>
public sealed record ResultadoDiagnosticoDto
{
    public DiagnosticoBusquedaDto? Diagnostico { get; init; }
    public IReadOnlyList<RepuestoDto> Piezas { get; init; } = [];
}

/// <summary>El diagnostico tal como queda guardado en la busqueda y se muestra en la UI.</summary>
public sealed record DiagnosticoBusquedaDto
{
    public required string Sistema { get; init; }
    public required string Urgencia { get; init; }
    public IReadOnlyList<string> Causas { get; init; } = [];
    public required string Fuente { get; init; }
}
