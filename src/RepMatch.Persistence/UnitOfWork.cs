using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence;

/// <summary>
/// Implementacion de <see cref="IUnitOfWork"/> sobre EF Core. El DbContext ya es una unidad de
/// trabajo: esta clase existe para que las capas superiores dependan de la abstraccion del
/// dominio y no del tipo de EF.
/// </summary>
public sealed class UnitOfWork(RepMatchDbContext contexto) : IUnitOfWork
{
    public Task<int> ConfirmarAsync(CancellationToken ct = default) =>
        contexto.SaveChangesAsync(ct);
}
