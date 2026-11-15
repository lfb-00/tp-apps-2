using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface INotificacionRepository
{
    Task AgregarAsync(Notificacion notificacion, CancellationToken ct = default);

    /// <summary>Las mas recientes primero. Lectura sin seguimiento: la bandeja se vuelve a pedir
    /// seguido y tiene que mostrar lo que hay en la base, no lo que cargo antes el mismo contexto.</summary>
    Task<IReadOnlyList<Notificacion>> ListarAsync(Guid destinatarioId, int maximo, CancellationToken ct = default);

    Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct = default);

    /// <summary>Las no leidas de esa persona sobre esa consulta, para marcarlas.</summary>
    Task<IReadOnlyList<Notificacion>> NoLeidasDeConsultaAsync(Guid destinatarioId, Guid busquedaId, CancellationToken ct = default);

    Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default);

    Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default);
}
