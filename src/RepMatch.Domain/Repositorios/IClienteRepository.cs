using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IClienteRepository
{
    Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Cliente?> ObtenerPorEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default);
    Task AgregarAsync(Cliente cliente, CancellationToken ct = default);
    void Eliminar(Cliente cliente);
}
