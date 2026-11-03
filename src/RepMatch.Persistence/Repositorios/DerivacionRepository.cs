using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class DerivacionRepository(RepMatchDbContext contexto) : IDerivacionRepository
{
    public async Task AgregarAsync(Derivacion derivacion, CancellationToken ct = default) =>
        await contexto.Derivaciones.AddAsync(derivacion, ct);

    public Task<int> ContarPorMecanicoAsync(Guid mecanicoId, CancellationToken ct = default) =>
        contexto.Derivaciones.CountAsync(d => d.MecanicoId == mecanicoId, ct);
}
