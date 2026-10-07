using Microsoft.EntityFrameworkCore;
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

    public async Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trabajo);

        // El proveedor InMemory (pruebas y modo sin Docker) no tiene transacciones.
        if (!contexto.Database.IsRelational())
        {
            await trabajo(ct);
            return;
        }

        var estrategia = contexto.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
            await trabajo(ct);
            await transaccion.CommitAsync(ct);
        });
    }
}
