namespace RepMatch.Domain.Repositorios;

/// <summary>
/// Confirma como una sola transaccion los cambios hechos a traves de los repositorios.
/// Se declara en el dominio y se implementa en el componente de persistencia: asi el dominio
/// no sabe que existe EF Core (inversion de dependencias).
/// </summary>
public interface IUnitOfWork
{
    Task<int> ConfirmarAsync(CancellationToken ct = default);

    /// <summary>Corre varias confirmaciones como una sola transaccion: o quedan todas o ninguna.
    /// Hace falta cuando un cambio no se puede ordenar en un solo guardado.</summary>
    Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> trabajo, CancellationToken ct = default);
}
