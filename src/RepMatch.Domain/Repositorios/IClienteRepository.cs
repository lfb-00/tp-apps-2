using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IClienteRepository
{
    Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Cliente?> ObtenerPorEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default);
    /// <summary>Directorio de mecanicos, opcionalmente de una provincia. Los del plan Pro van
    /// primero: es el "perfil destacado" que paga el profesional, no un cambio en el ranking de
    /// ofertas.</summary>
    Task<IReadOnlyList<Cliente>> ListarMecanicosAsync(string? provincia = null, CancellationToken ct = default);

    Task AgregarAsync(Cliente cliente, CancellationToken ct = default);
    void Eliminar(Cliente cliente);
}
