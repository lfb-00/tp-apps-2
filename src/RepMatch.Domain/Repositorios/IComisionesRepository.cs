using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IComisionesRepository
{
    Task<ComisionesTienda?> ObtenerAsync(string tienda, string periodo, CancellationToken ct = default);

    Task AgregarAsync(ComisionesTienda comisiones, CancellationToken ct = default);

    /// <summary>Del mes mas reciente al mas viejo y, dentro de cada mes, por tienda.</summary>
    Task<IReadOnlyList<ComisionesTienda>> ListarAsync(CancellationToken ct = default);

    Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default);

    Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default);
}
