using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class ConfiguracionVehiculoRepository(RepMatchDbContext contexto) : IConfiguracionVehiculoRepository
{
    public async Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct = default) =>
        await contexto.ConfiguracionesVehiculo
            .OrderBy(c => c.Marca).ThenBy(c => c.Modelo).ThenBy(c => c.AnioDesde).ThenBy(c => c.Motor)
            .ToListAsync(ct);

    public Task<bool> ExisteAsync(string marca, string modelo, int anio, string? motor, CancellationToken ct = default) =>
        contexto.ConfiguracionesVehiculo.AnyAsync(c =>
            c.Marca.ToLower() == marca.Trim().ToLower()
            && c.Modelo.ToLower() == modelo.Trim().ToLower()
            && c.AnioDesde <= anio && c.AnioHasta >= anio
            && (string.IsNullOrWhiteSpace(motor) || c.Motor.ToLower() == motor.Trim().ToLower()), ct);

    public Task AgregarRangoAsync(IEnumerable<ConfiguracionVehiculo> configuraciones, CancellationToken ct = default) =>
        contexto.ConfiguracionesVehiculo.AddRangeAsync(configuraciones, ct);
}
