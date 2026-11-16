using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class CasaRepuestosRepository(RepMatchDbContext contexto) : ICasaRepuestosRepository
{
    public async Task<IReadOnlyList<CasaRepuestos>> EnRectanguloAsync(
        double latitudMinima, double latitudMaxima, double longitudMinima, double longitudMaxima,
        CancellationToken ct = default) =>
        await contexto.CasasRepuestos.AsNoTracking()
            .Where(c => c.Latitud >= latitudMinima && c.Latitud <= latitudMaxima
                        && c.Longitud >= longitudMinima && c.Longitud <= longitudMaxima)
            .ToListAsync(ct);

    public Task<int> ContarAsync(CancellationToken ct = default) => contexto.CasasRepuestos.CountAsync(ct);

    public async Task<DateTimeOffset?> UltimaImportacionAsync(CancellationToken ct = default) =>
        await contexto.CasasRepuestos.AnyAsync(ct)
            ? await contexto.CasasRepuestos.MaxAsync(c => c.ImportadaEn, ct)
            : null;

    public async Task ReemplazarTodasAsync(IReadOnlyCollection<CasaRepuestos> casas, CancellationToken ct = default)
    {
        // Se cargan y se borran en vez de ExecuteDelete: el borrado y el alta quedan en el mismo
        // SaveChanges (una transaccion), y si algo falla queda la copia anterior. Son unos pocos
        // miles de filas.
        contexto.CasasRepuestos.RemoveRange(await contexto.CasasRepuestos.ToListAsync(ct));
        await contexto.CasasRepuestos.AddRangeAsync(casas, ct);
    }
}
