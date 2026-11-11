using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class ComisionesRepository(RepMatchDbContext contexto) : IComisionesRepository
{
    public Task<ComisionesTienda?> ObtenerAsync(string tienda, string periodo, CancellationToken ct = default) =>
        contexto.Comisiones.FirstOrDefaultAsync(c => c.Tienda == tienda && c.Periodo == periodo, ct);

    public async Task AgregarAsync(ComisionesTienda comisiones, CancellationToken ct = default) =>
        await contexto.Comisiones.AddAsync(comisiones, ct);

    public async Task<IReadOnlyList<ComisionesTienda>> ListarAsync(CancellationToken ct = default) =>
        await contexto.Comisiones.AsNoTracking()
            .OrderByDescending(c => c.Periodo)
            .ThenBy(c => c.Tienda)
            .ToListAsync(ct);

    public Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default) =>
        contexto.MensajesProcesados.AnyAsync(m => m.MensajeId == mensajeId && m.Consumidor == consumidor, ct);

    public async Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default) =>
        await contexto.MensajesProcesados.AddAsync(marca, ct);
}
