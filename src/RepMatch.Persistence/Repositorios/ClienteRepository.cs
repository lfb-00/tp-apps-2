using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class ClienteRepository(RepMatchDbContext contexto) : IClienteRepository
{
    public Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        contexto.Clientes
            .Include(c => c.Vehiculos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Cliente?> ObtenerPorEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        return contexto.Clientes
            .Include(c => c.Vehiculos)
            .FirstOrDefaultAsync(c => c.Email == normalizado, ct);
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default) =>
        await contexto.Clientes
            .Include(c => c.Vehiculos)
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);

    public async Task AgregarAsync(Cliente cliente, CancellationToken ct = default) =>
        await contexto.Clientes.AddAsync(cliente, ct);

    public void Eliminar(Cliente cliente) => contexto.Clientes.Remove(cliente);
}
