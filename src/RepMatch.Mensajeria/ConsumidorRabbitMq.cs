using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;
using Serilog.Context;

namespace RepMatch.Mensajeria;

/// <summary>Que hacer con un mensaje despues de intentar procesarlo.</summary>
public enum DecisionEntrega
{
    /// <summary>Procesado: se confirma y sale de la cola.</summary>
    Confirmar,

    /// <summary>Fallo por primera vez: vuelve a la cola para un segundo intento.</summary>
    Reintentar,

    /// <summary>No se va a poder procesar (mal formado, o ya fallo dos veces): va a la DLQ.</summary>
    ADlq
}

public static class ReglaEntrega
{
    /// <summary>
    /// La regla de reintento, aparte del transporte para poder probarla sin broker. Un intento mas
    /// cubre las fallas pasajeras (la base reiniciandose); mas intentos sobre un error permanente
    /// solo lo repiten. El mensaje que no se puede leer no se reintenta: va a fallar siempre igual.
    /// </summary>
    public static DecisionEntrega Decidir(bool sePudoLeer, bool sePudoProcesar, bool yaSeReintento) =>
        !sePudoLeer ? DecisionEntrega.ADlq
        : sePudoProcesar ? DecisionEntrega.Confirmar
        : yaSeReintento ? DecisionEntrega.ADlq
        : DecisionEntrega.Reintentar;
}

/// <summary>
/// Base de un consumidor de la cola (issue #14). Cada consumidor tiene su cola durable y su DLQ
/// (<see cref="TopologiaRabbitMq"/>); esta clase resuelve lo que es igual para todos:
/// <list type="bullet">
/// <item>Conecta cuando el broker esta disponible y reintenta si no, sin tumbar el proceso.</item>
/// <item>Confirma (ack) recien despues de procesar: si el proceso muere a mitad, el mensaje vuelve.</item>
/// <item>Reintenta una vez y despues manda a la DLQ (<see cref="ReglaEntrega"/>).</item>
/// <item>Pone en el contexto de log el correlation id que vino en el mensaje.</item>
/// <item>Abre un scope de DI por mensaje: cada uno usa su propio DbContext.</item>
/// </list>
/// El mismo mensaje puede llegar dos veces (por el reintento o por una reconexion): la
/// idempotencia es responsabilidad de <see cref="ProcesarAsync"/>, por <c>MensajeId</c>.
/// </summary>
public abstract class ConsumidorRabbitMq<T>(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    IServiceScopeFactory scopes,
    ILogger log) : BackgroundService
{
    private static readonly TimeSpan EsperaReconexion = TimeSpan.FromSeconds(5);

    protected abstract DefinicionCola Cola { get; }

    protected abstract Task ProcesarAsync(IServiceProvider servicios, MensajeIntegracion<T> mensaje, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Habilitada)
        {
            log.LogWarning("Mensajeria desactivada (Mensajeria:Habilitada=false): la cola {Cola} no se consume", Cola.Nombre);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conectada = await conexion.ObtenerAsync($"RepMatch consumidor {Cola.Nombre}", stoppingToken);
                await using var canal = await conectada.CreateChannelAsync(cancellationToken: stoppingToken);
                await TopologiaRabbitMq.DeclararColaAsync(canal, opciones, Cola, stoppingToken);
                await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, stoppingToken);

                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.ReceivedAsync += (_, entrega) => AlRecibirAsync(canal, entrega, stoppingToken);
                await canal.BasicConsumeAsync(Cola.Nombre, autoAck: false, consumidor, stoppingToken);

                log.LogInformation("Escuchando la cola {Cola} ({Claves})", Cola.Nombre, string.Join(", ", Cola.ClavesRuteo));

                // La recuperacion automatica del cliente reconecta y vuelve a consumir; aca solo se
                // sale si se cierra el canal para siempre o si el proceso se detiene.
                while (canal.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Sin conexion para la cola {Cola}; reintento en {Segundos}s",
                    Cola.Nombre, EsperaReconexion.TotalSeconds);
                await Task.Delay(EsperaReconexion, stoppingToken);
            }
        }
    }

    private async Task AlRecibirAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        MensajeIntegracion<T>? mensaje = null;
        var procesado = false;

        try
        {
            mensaje = SerializacionMensajes.Deserializar<T>(entrega.Body.Span);
        }
        catch (JsonException ex)
        {
            log.LogError(ex, "Mensaje ilegible en {Cola} (MessageId {MessageId}): va a la DLQ",
                Cola.Nombre, entrega.BasicProperties.MessageId);
        }

        if (mensaje is not null)
        {
            var correlationId = mensaje.CorrelationId ?? LeerCorrelacion(entrega);
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (ContextoCorrelacion.Establecer(correlationId))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await ProcesarAsync(scope.ServiceProvider, mensaje, ct);
                    procesado = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    log.LogError(ex, "Fallo {Tipo} {MensajeId} en {Cola} (reintento: {Reintento})",
                        mensaje.Tipo, mensaje.MensajeId, Cola.Nombre, entrega.Redelivered);
                }
            }
        }

        switch (ReglaEntrega.Decidir(mensaje is not null, procesado, entrega.Redelivered))
        {
            case DecisionEntrega.Confirmar:
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                break;
            case DecisionEntrega.Reintentar:
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, ct);
                break;
            default:
                log.LogWarning("Mensaje {MessageId} enviado a {Dlq}", entrega.BasicProperties.MessageId, Cola.NombreDlq);
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false, ct);
                break;
        }
    }

    private static string? LeerCorrelacion(BasicDeliverEventArgs entrega) =>
        entrega.BasicProperties.Headers?.TryGetValue(CorrelacionMiddleware.NombreCabecera, out var valor) == true
            ? valor switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string texto => texto,
                _ => null
            }
            : null;
}
