using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

/// <summary>
/// Lo que el directorio muestra de un mecanico. Incluye el contacto porque el cliente lo elige
/// para que lo atienda; no incluye email ni garage.
/// </summary>
public sealed record MecanicoDto
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public string? NombreTaller { get; init; }
    public string? Provincia { get; init; }
    public string? Localidad { get; init; }
    public string? Telefono { get; init; }
    public bool TieneWhatsApp { get; init; }
    public string? FotoUrl { get; init; }
    public IReadOnlyList<string> Especialidades { get; init; } = [];
    public required string Plan { get; init; }

    /// <summary>Perfil destacado del plan Pro: sale primero en el directorio.</summary>
    public bool Destacado => Plan == PlanesMecanico.Pro;

    public string NombreVisible => NombreTaller ?? Nombre;
}

public sealed record ActualizarTallerDto
{
    [StringLength(120)]
    public string? NombreTaller { get; init; }

    /// <summary>Nombres de sistema del catalogo (Frenos, Motor, ...).</summary>
    public IReadOnlyList<string> Especialidades { get; init; } = [];
}

/// <summary>
/// Revision del mecanico. Si confirma, recomienda lo que propuso el sistema y los codigos se
/// ignoran; si corrige, tiene que indicar al menos uno.
/// </summary>
public sealed record RevisarBusquedaDto
{
    public required bool ConfirmaDiagnostico { get; init; }
    public IReadOnlyList<string> CodigosRecomendados { get; init; } = [];

    [StringLength(1000)]
    public string? Nota { get; init; }
}

public sealed record RevisionDto
{
    public required bool ConfirmaDiagnostico { get; init; }
    public IReadOnlyList<string> CodigosRecomendados { get; init; } = [];
    public string? Nota { get; init; }
    public required DateTimeOffset RevisadaEn { get; init; }
}

/// <summary>
/// Una busqueda vista desde la colaboracion cliente-mecanico: lo que ve el cliente en el
/// detalle (con el mecanico que la tiene) y lo que ve el mecanico en su bandeja (con el nombre
/// del cliente, sin el resto de sus datos).
/// </summary>
public sealed record ConsultaDto
{
    public required BusquedaDto Busqueda { get; init; }
    public required string NombreCliente { get; init; }

    /// <summary>null si no esta compartida o si el mecanico ya no tiene cuenta.</summary>
    public MecanicoDto? Mecanico { get; init; }
}

public sealed record BandejaMecanicoDto
{
    public IReadOnlyList<ConsultaDto> Consultas { get; init; } = [];
    public required int Pendientes { get; init; }

    /// <summary>Tope de pendientes del plan; null es sin limite (Pro).</summary>
    public int? LimitePendientes { get; init; }

    /// <summary>Derivaciones a tiendas atribuidas a sus recomendaciones.</summary>
    public required int DerivacionesGeneradas { get; init; }
}
