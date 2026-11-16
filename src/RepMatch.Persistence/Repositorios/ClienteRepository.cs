using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class ClienteRepository(RepMatchDbContext contexto) : IClienteRepository
{
    public async Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        // Mismo motivo que BusquedaRepository.ObtenerPorIdAsync: en Blazor Server el plan del
        // mecanico puede cambiar desde otro circuito y la entidad trackeada quedaria desactualizada.
        var entrada = contexto.ChangeTracker.Entries<Cliente>().FirstOrDefault(e => e.Entity.Id == id);
        if (entrada is not null)
            await entrada.ReloadAsync(ct);

        return await contexto.Clientes
            .Include(c => c.Vehiculos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

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

    public async Task<IReadOnlyList<Cliente>> ListarMecanicosAsync(
        string? provincia = null, CancellationToken ct = default)
    {
        var mecanicos = contexto.Clientes.Where(c => c.Rol == RolUsuario.Mecanico);

        if (!string.IsNullOrWhiteSpace(provincia))
        {
            var buscada = provincia.Trim();
            mecanicos = mecanicos.Where(c => c.Provincia == buscada);
        }

        return await mecanicos
            .OrderByDescending(c => c.Plan == PlanMecanico.Pro)
            .ThenBy(c => c.NombreTaller ?? c.Nombre)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(Cliente cliente, CancellationToken ct = default) =>
        await contexto.Clientes.AddAsync(cliente, ct);

    public void Eliminar(Cliente cliente) => contexto.Clientes.Remove(cliente);
}
