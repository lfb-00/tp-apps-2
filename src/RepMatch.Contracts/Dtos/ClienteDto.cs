using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Dtos;

public sealed record ClienteDto
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset FechaAlta { get; init; }
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
}
