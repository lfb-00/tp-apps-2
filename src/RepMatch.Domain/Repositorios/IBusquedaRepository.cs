using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface IBusquedaRepository
{
    Task<Busqueda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<Busqueda>> ListarRecientesAsync(int cantidad = 20, CancellationToken ct = default);
    Task AgregarAsync(Busqueda busqueda, CancellationToken ct = default);

    /// <summary>Bandeja del mecanico: lo que le compartieron, las sin revisar primero.</summary>
    Task<IReadOnlyList<Busqueda>> ListarCompartidasConAsync(Guid mecanicoId, CancellationToken ct = default);

    Task<int> ContarPendientesDeRevisionAsync(Guid mecanicoId, CancellationToken ct = default);

    /// <summary>La busqueda a la que pertenece una oferta, para registrar la derivacion.</summary>
    Task<Busqueda?> ObtenerPorOfertaAsync(Guid ofertaId, CancellationToken ct = default);

    /// <summary>Marca para borrar todas las busquedas del cliente. Se confirma con la unidad de
    /// trabajo: busquedas.ClienteId no tiene FK a clientes, asi que no caen por cascada.</summary>
    Task<int> EliminarPorClienteAsync(Guid clienteId, CancellationToken ct = default);
}
