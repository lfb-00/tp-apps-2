using Microsoft.Extensions.DependencyInjection;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Mensajeria;

namespace RepMatch.Notificaciones.Worker.Consumidores;

/// <summary>
/// Cola <c>repmatch.avisos.compartidas</c>: cada <c>busqueda.compartida</c> deja un aviso al
/// mecanico. El cliente que compartio no espera a que se escriba; si el worker esta caido, el
/// aviso llega cuando vuelve.
/// </summary>
public sealed class ConsumidorAvisosCompartidas(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorAvisosCompartidas> log)
    : ConsumidorRabbitMq<BusquedaCompartidaV1>(conexion, opciones, scopes, log)
{
    public static readonly DefinicionCola Definicion = TopologiaRabbitMq.ColaAvisosCompartidas;

    protected override DefinicionCola Cola => Definicion;

    protected override Task ProcesarAsync(
        IServiceProvider servicios, MensajeIntegracion<BusquedaCompartidaV1> mensaje, CancellationToken ct) =>
        servicios.GetRequiredService<ServicioNotificaciones>().AvisarCompartidaAsync(mensaje, ct);
}

/// <summary>Cola <c>repmatch.avisos.revisiones</c>: cada <c>diagnostico.validado</c> deja un aviso al
/// cliente con lo que hizo el mecanico (confirmo o corrigio).</summary>
public sealed class ConsumidorAvisosRevisiones(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorAvisosRevisiones> log)
    : ConsumidorRabbitMq<DiagnosticoValidadoV1>(conexion, opciones, scopes, log)
{
    public static readonly DefinicionCola Definicion = TopologiaRabbitMq.ColaAvisosRevisiones;

    protected override DefinicionCola Cola => Definicion;

    protected override Task ProcesarAsync(
        IServiceProvider servicios, MensajeIntegracion<DiagnosticoValidadoV1> mensaje, CancellationToken ct) =>
        servicios.GetRequiredService<ServicioNotificaciones>().AvisarRevisadaAsync(mensaje, ct);
}
