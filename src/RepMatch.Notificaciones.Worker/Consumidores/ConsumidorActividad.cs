using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Mensajeria;

namespace RepMatch.Notificaciones.Worker.Consumidores;

/// <summary>
/// Cola <c>repmatch.actividad</c>: recibe todos los eventos (clave <c>#</c>) y los deja en el log
/// con su correlation id. Es el registro de actividad del sistema y lo que muestra, en la demo, que
/// una accion en la Web llega hasta el consumidor con el mismo id. Es tambien el lugar donde se
/// enchufan los proximos consumidores, por ejemplo las notificaciones a cliente y mecanico: una
/// cola nueva atada a <c>busqueda.compartida</c> o <c>diagnostico.validado</c>.
///
/// Lee los datos como JSON generico porque escucha tipos distintos; solo registra, asi que una
/// segunda entrega del mismo mensaje no tiene efecto que deduplicar.
/// </summary>
public sealed class ConsumidorActividad(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorActividad> log)
    : ConsumidorRabbitMq<JsonElement>(conexion, opciones, scopes, log)
{
    public static readonly DefinicionCola Definicion = new("repmatch.actividad", ["#"]);

    protected override DefinicionCola Cola => Definicion;

    protected override Task ProcesarAsync(
        IServiceProvider servicios, MensajeIntegracion<JsonElement> mensaje, CancellationToken ct)
    {
        log.LogInformation(
            "Evento {Tipo} v{Version} {MensajeId} ocurrido {OcurridoEn:O}: {Datos}",
            mensaje.Tipo, mensaje.Version, mensaje.MensajeId, mensaje.OcurridoEn, mensaje.Datos.GetRawText());
        return Task.CompletedTask;
    }
}
