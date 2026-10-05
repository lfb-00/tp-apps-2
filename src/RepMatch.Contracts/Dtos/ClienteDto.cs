using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

public sealed record ClienteDto
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset FechaAlta { get; init; }
    public string? Telefono { get; init; }
    public bool TieneWhatsApp { get; init; }
    public string? Provincia { get; init; }
    public string? Localidad { get; init; }

    /// <summary>Foto como data URL (data:image/jpeg;base64,...), lista para un &lt;img&gt;.
    /// null si no tiene.</summary>
    public string? FotoUrl { get; init; }

    public Guid? VehiculoPredeterminadoId { get; init; }

    /// <summary>"claro", "oscuro" o null si nunca eligio.</summary>
    public string? TemaPreferido { get; init; }

    public IReadOnlyList<VehiculoClienteDto> Vehiculos { get; init; } = [];
}

public sealed record VehiculoClienteDto
{
    public required Guid Id { get; init; }
    public required string Alias { get; init; }
    public required VehiculoDto Datos { get; init; }
    public string? Vin { get; init; }
}

public sealed record CrearClienteDto
{
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Nombre { get; init; }

    [Required, EmailAddress, StringLength(200)]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 8)]
    public required string Contrasena { get; init; }
}

public sealed record LoginDto
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Contrasena { get; init; }
}

/// <summary>Datos editables del perfil. El email no esta: no se puede cambiar.</summary>
public sealed record ActualizarPerfilDto
{
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Nombre { get; init; }

    [StringLength(20, MinimumLength = 8)]
    [RegularExpression(@"^\+?[0-9 ()-]+$")]
    public string? Telefono { get; init; }

    public bool TieneWhatsApp { get; init; }

    [StringLength(60)]
    public string? Provincia { get; init; }

    [StringLength(100)]
    public string? Localidad { get; init; }
}

public sealed record CambiarContrasenaDto
{
    [Required]
    public required string ContrasenaActual { get; init; }

    [Required, StringLength(100, MinimumLength = 8)]
    public required string ContrasenaNueva { get; init; }

    [Required, Compare(nameof(ContrasenaNueva))]
    public required string RepetirContrasena { get; init; }
}

public sealed record CambiarFotoPerfilDto
{
    [Required]
    public required byte[] Datos { get; init; }

    [Required, StringLength(20)]
    public required string TipoContenido { get; init; }
}

public sealed record EliminarCuentaDto
{
    [Required]
    public required string Contrasena { get; init; }
}
