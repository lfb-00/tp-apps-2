using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class BusquedaRepository(RepMatchDbContext contexto) : IBusquedaRepository
{
    public Task<Busqueda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        contexto.Busquedas
            .Include(b => b.Ofertas)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Busqueda>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken ct = default) =>
        await contexto.Busquedas
            .Include(b => b.Ofertas)
            .Where(b => b.ClienteId == clienteId)
            .OrderByDescending(b => b.FechaCreacion)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Busqueda>> ListarRecientesAsync(
        int cantidad = 20, CancellationToken ct = default) =>
        await contexto.Busquedas
            .Include(b => b.Ofertas)
            .OrderByDescending(b => b.FechaCreacion)
            .Take(cantidad)
            .ToListAsync(ct);

    public async Task AgregarAsync(Busqueda busqueda, CancellationToken ct = default) =>
        await contexto.Busquedas.AddAsync(busqueda, ct);
}
