using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IDerivacionRepository
{
    Task AgregarAsync(Derivacion derivacion, CancellationToken ct = default);

    /// <summary>Derivaciones atribuidas al mecanico: cuanto negocio genero con sus
    /// recomendaciones.</summary>
    Task<int> ContarPorMecanicoAsync(Guid mecanicoId, CancellationToken ct = default);
}
