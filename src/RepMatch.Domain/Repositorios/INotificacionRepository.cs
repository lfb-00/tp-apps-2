using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface INotificacionRepository
{
    Task AgregarAsync(Notificacion notificacion, CancellationToken ct = default);

    /// <summary>Las mas recientes primero, solo de consultas que la persona todavia puede abrir.
    /// Lectura sin seguimiento: la bandeja se vuelve a pedir seguido y tiene que mostrar lo que hay
    /// en la base, no lo que cargo antes el mismo contexto.</summary>
    Task<IReadOnlyList<Notificacion>> ListarAsync(Guid destinatarioId, int maximo, CancellationToken ct = default);

    /// <summary>No leidas, con el mismo filtro que <see cref="ListarAsync"/>: el aviso de una
    /// consulta que ya no se puede abrir tampoco se puede marcar leido, asi que no cuenta.</summary>
    Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct = default);

    /// <summary>Al borrar una cuenta: los avisos que recibio y los que generaron sus consultas.
    /// Deja el borrado en la unidad de trabajo y devuelve cuantos son.</summary>
    Task<int> EliminarDePersonaAsync(Guid personaId, CancellationToken ct = default);

    /// <summary>Las no leidas de esa persona sobre esa consulta, para marcarlas.</summary>
    Task<IReadOnlyList<Notificacion>> NoLeidasDeConsultaAsync(Guid destinatarioId, Guid busquedaId, CancellationToken ct = default);

    Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default);

    Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default);
}
