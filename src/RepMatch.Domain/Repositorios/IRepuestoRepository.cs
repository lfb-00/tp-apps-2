using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Repositorios;

public interface IRepuestoRepository
{
    Task<Repuesto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Repuesto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default);
    Task<IReadOnlyList<Repuesto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Repuestos cuya tabla de aplicaciones cubre el vehiculo indicado. Es la consulta
    /// que despues expone el servicio SOAP ConsultaCompatibilidad.</summary>
    Task<IReadOnlyList<Repuesto>> BuscarCompatiblesAsync(
        DatosVehiculo vehiculo, SistemaVehiculo? sistema = null, CancellationToken ct = default);

    Task AgregarAsync(Repuesto repuesto, CancellationToken ct = default);
}
