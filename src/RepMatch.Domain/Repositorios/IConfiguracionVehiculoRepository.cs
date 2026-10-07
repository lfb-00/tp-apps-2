using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IConfiguracionVehiculoRepository
{
    Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct = default);
    Task<bool> ExisteAsync(string marca, string modelo, int anio, string? motor, CancellationToken ct = default);
    Task AgregarRangoAsync(IEnumerable<ConfiguracionVehiculo> configuraciones, CancellationToken ct = default);
}
