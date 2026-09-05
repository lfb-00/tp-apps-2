namespace RepMatch.Domain.Repositorios;

/// <summary>
/// Confirma como una sola transaccion los cambios hechos a traves de los repositorios.
/// Se declara en el dominio y se implementa en el componente de persistencia: asi el dominio
/// no sabe que existe EF Core (inversion de dependencias).
/// </summary>
public interface IUnitOfWork
{
    Task<int> ConfirmarAsync(CancellationToken ct = default);
}
