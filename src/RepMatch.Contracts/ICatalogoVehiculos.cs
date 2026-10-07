using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>Catálogo de configuraciones de vehículos disponibles para la interfaz.</summary>
public interface ICatalogoVehiculos
{
    Task<IReadOnlyList<OpcionVehiculoDto>> ListarOpcionesAsync(CancellationToken ct = default);
    Task<bool> EsConfiguracionValidaAsync(VehiculoDto vehiculo, CancellationToken ct = default);
}
