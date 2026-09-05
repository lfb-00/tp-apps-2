using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IBusquedaRepository
{
    Task<Busqueda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarRecientesAsync(int cantidad = 20, CancellationToken ct = default);
    Task AgregarAsync(Busqueda busqueda, CancellationToken ct = default);
}
