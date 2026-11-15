using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence.Repositorios;

public sealed class NotificacionRepository(RepMatchDbContext contexto) : INotificacionRepository
{
    public async Task AgregarAsync(Notificacion notificacion, CancellationToken ct = default) =>
        await contexto.Notificaciones.AddAsync(notificacion, ct);

    public async Task<IReadOnlyList<Notificacion>> ListarAsync(Guid destinatarioId, int maximo, CancellationToken ct = default) =>
        await contexto.Notificaciones.AsNoTracking()
            .Where(n => n.DestinatarioId == destinatarioId)
            .OrderByDescending(n => n.CreadaEn)
            .Take(maximo)
            .ToListAsync(ct);

    public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct = default) =>
        contexto.Notificaciones.CountAsync(n => n.DestinatarioId == destinatarioId && n.LeidaEn == null, ct);

    public async Task<IReadOnlyList<Notificacion>> NoLeidasDeConsultaAsync(
        Guid destinatarioId, Guid busquedaId, CancellationToken ct = default) =>
        await contexto.Notificaciones
            .Where(n => n.DestinatarioId == destinatarioId && n.BusquedaId == busquedaId && n.LeidaEn == null)
            .ToListAsync(ct);

    public Task<bool> YaProcesadoAsync(Guid mensajeId, string consumidor, CancellationToken ct = default) =>
        contexto.MensajesProcesados.AnyAsync(m => m.MensajeId == mensajeId && m.Consumidor == consumidor, ct);

    public async Task MarcarProcesadoAsync(MensajeProcesado marca, CancellationToken ct = default) =>
        await contexto.MensajesProcesados.AddAsync(marca, ct);
}
