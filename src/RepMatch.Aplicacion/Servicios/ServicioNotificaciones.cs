using Microsoft.Extensions.Logging;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Avisos a cliente y mecanico, alimentados por la cola: <c>busqueda.compartida</c> avisa al
/// mecanico y <c>diagnostico.validado</c> avisa al cliente. Son los consumidores que la seccion 7
/// del informe dejaba previstos sobre la misma base (productores, topologia y
/// <c>ConsumidorRabbitMq</c> sin cambios). Idempotente por <c>MensajeId</c>, como las comisiones.
/// </summary>
public sealed class ServicioNotificaciones(
    INotificacionRepository notificaciones,
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
    IUnitOfWork unidadDeTrabajo,
    ILogger<ServicioNotificaciones> log)
{
    public const string Consumidor = "notificaciones";
    public const int MaximoRecientes = 10;

    public Task<bool> AvisarCompartidaAsync(MensajeIntegracion<BusquedaCompartidaV1> mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        return RegistrarAsync(mensaje.MensajeId, mensaje.OcurridoEn, mensaje.Datos.MecanicoId,
            TipoNotificacion.ConsultaCompartida, mensaje.Datos.BusquedaId, ct);
    }

    public Task<bool> AvisarRevisadaAsync(MensajeIntegracion<DiagnosticoValidadoV1> mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var tipo = mensaje.Datos.Confirmado ? TipoNotificacion.DiagnosticoConfirmado : TipoNotificacion.DiagnosticoCorregido;
        return RegistrarAsync(mensaje.MensajeId, mensaje.OcurridoEn, mensaje.Datos.ClienteId,
            tipo, mensaje.Datos.BusquedaId, ct);
    }

    public async Task<BandejaNotificacionesDto> ObtenerBandejaAsync(Guid destinatarioId, CancellationToken ct = default)
    {
        var recientes = await notificaciones.ListarAsync(destinatarioId, MaximoRecientes, ct);
        var dtos = new List<NotificacionDto>(recientes.Count);
        foreach (var n in recientes)
            dtos.Add(await ADtoAsync(n, ct));

        return new BandejaNotificacionesDto
        {
            NoLeidas = await notificaciones.ContarNoLeidasAsync(destinatarioId, ct),
            Recientes = dtos
        };
    }

    public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct = default) =>
        notificaciones.ContarNoLeidasAsync(destinatarioId, ct);

    /// <summary>Abrir la consulta es leer sus avisos. Devuelve cuantos marco.</summary>
    public async Task<int> MarcarLeidasAsync(Guid destinatarioId, Guid busquedaId, CancellationToken ct = default)
    {
        var pendientes = await notificaciones.NoLeidasDeConsultaAsync(destinatarioId, busquedaId, ct);
        if (pendientes.Count == 0)
            return 0;

        var ahora = DateTimeOffset.UtcNow;
        foreach (var n in pendientes)
            n.MarcarLeida(ahora);
        await unidadDeTrabajo.ConfirmarAsync(ct);
        return pendientes.Count;
    }

    private async Task<bool> RegistrarAsync(
        Guid mensajeId, DateTimeOffset ocurridoEn, Guid destinatarioId, TipoNotificacion tipo, Guid busquedaId,
        CancellationToken ct)
    {
        if (await notificaciones.YaProcesadoAsync(mensajeId, Consumidor, ct))
        {
            log.LogInformation("Aviso del mensaje {MensajeId} ya registrado: se ignora la segunda entrega", mensajeId);
            return false;
        }

        await notificaciones.AgregarAsync(new Notificacion(destinatarioId, tipo, busquedaId, ocurridoEn), ct);
        await notificaciones.MarcarProcesadoAsync(new MensajeProcesado(mensajeId, Consumidor), ct);
        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation("Aviso {Tipo} registrado para {Destinatario} sobre la consulta {BusquedaId}",
            tipo, destinatarioId, busquedaId);
        return true;
    }

    private async Task<NotificacionDto> ADtoAsync(Notificacion n, CancellationToken ct)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(n.BusquedaId, ct);
        var auto = busqueda is null ? null : $"{busqueda.Vehiculo.Marca} {busqueda.Vehiculo.Modelo} {busqueda.Vehiculo.Anio}";
        // La bandeja ya no lista avisos de consultas borradas, pero entre leer la lista y armar el
        // texto la consulta se puede borrar: el respaldo tiene que leerse bien igual.
        var suVehiculo = auto is null ? "una consulta" : $"su {auto}";
        var tuVehiculo = auto is null ? "tu consulta" : $"tu {auto}";

        string titulo;
        string detalle;
        switch (n.Tipo)
        {
            case TipoNotificacion.ConsultaCompartida:
                var cliente = busqueda is null ? null : await clientes.ObtenerPorIdAsync(busqueda.ClienteId, ct);
                titulo = "Nueva consulta para revisar";
                detalle = $"{cliente?.Nombre ?? "Un cliente"} te compartió {suVehiculo}.";
                break;
            default:
                var mecanico = busqueda?.MecanicoId is { } id ? await clientes.ObtenerPorIdAsync(id, ct) : null;
                var quien = mecanico?.NombreTaller ?? mecanico?.Nombre ?? "El mecánico";
                titulo = n.Tipo == TipoNotificacion.DiagnosticoConfirmado ? "Diagnóstico confirmado" : "Diagnóstico corregido";
                detalle = n.Tipo == TipoNotificacion.DiagnosticoConfirmado
                    ? $"{quien} confirmó el diagnóstico de {tuVehiculo}."
                    : $"{quien} recomendó otras piezas para {tuVehiculo}.";
                break;
        }

        return new NotificacionDto
        {
            Id = n.Id,
            BusquedaId = n.BusquedaId,
            Titulo = titulo,
            Detalle = detalle,
            CreadaEn = n.CreadaEn,
            Leida = n.Leida
        };
    }
}
