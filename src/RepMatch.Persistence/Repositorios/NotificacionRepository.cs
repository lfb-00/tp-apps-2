using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class NotificacionRepository(RepMatchDbContext contexto) : INotificacionRepository
{
    public async Task AgregarAsync(Notificacion notificacion, CancellationToken ct = default) =>
        await contexto.Notificaciones.AddAsync(notificacion, ct);

    public async Task<IReadOnlyList<Notificacion>> ListarAsync(Guid destinatarioId, int maximo, CancellationToken ct = default) =>
        await Visibles(destinatarioId).AsNoTracking()
            .OrderByDescending(n => n.CreadaEn)
            .Take(maximo)
            .ToListAsync(ct);

    public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct = default) =>
        Visibles(destinatarioId).CountAsync(n => n.LeidaEn == null, ct);

    public async Task<IReadOnlyList<Notificacion>> NoLeidasDeConsultaAsync(
        Guid destinatarioId, Guid busquedaId, CancellationToken ct = default) =>
        await contexto.Notificaciones
            .Where(n => n.DestinatarioId == destinatarioId && n.BusquedaId == busquedaId && n.LeidaEn == null)
            .ToListAsync(ct);

    public async Task<int> EliminarDePersonaAsync(Guid personaId, CancellationToken ct = default)
    {
        // Se cargan en vez de usar ExecuteDelete, igual que las busquedas: el borrado queda en la
        // misma unidad de trabajo que el de la cuenta. Se consulta antes de guardar el borrado de
        // sus busquedas, asi que todavia se encuentran los avisos que generaron.
        var deLaPersona = await contexto.Notificaciones
            .Where(n => n.DestinatarioId == personaId
                        || contexto.Busquedas.Any(b => b.Id == n.BusquedaId && b.ClienteId == personaId))
            .ToListAsync(ct);

        contexto.Notificaciones.RemoveRange(deLaPersona);
        return deLaPersona.Count;
    }

    public Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default) =>
        contexto.MensajesProcesados.AnyAsync(m => m.MensajeId == mensajeId && m.Consumidor == consumidor, ct);

    public async Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default) =>
        await contexto.MensajesProcesados.AddAsync(marca, ct);

    /// <summary>
    /// Los avisos de esa persona sobre consultas que todavia puede abrir: la consulta existe y es
    /// su duenia o el mecanico al que esta compartida (la misma regla que
    /// <c>ServicioRevisiones.ObtenerAsync</c>). Un aviso solo se marca leido al abrir su consulta;
    /// si el cliente dejo de compartirla (incluso antes de que el worker escribiera el aviso) o
    /// borro su cuenta, ya no se puede abrir, y sin este filtro la campana quedaria con un aviso
    /// sin leer para siempre.
    /// </summary>
    private IQueryable<Notificacion> Visibles(Guid destinatarioId) =>
        contexto.Notificaciones.Where(n => n.DestinatarioId == destinatarioId
            && contexto.Busquedas.Any(b => b.Id == n.BusquedaId
                && (b.ClienteId == destinatarioId || b.MecanicoId == destinatarioId)));
}
