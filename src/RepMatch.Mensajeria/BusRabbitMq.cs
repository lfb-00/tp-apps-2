using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;

namespace RepMatch.Mensajeria;

/// <summary>
/// <see cref="IEventBus"/> sobre RabbitMQ.Client (issue #12). Publica en el exchange topic con la
/// clave de ruteo del mensaje, con el mensaje marcado persistente y esperando la confirmacion del
/// broker (publisher confirms): si el broker no lo confirma, se lanza y quien publica lo registra.
///
/// Un solo canal de publicacion compartido, usado de a uno: los canales de RabbitMQ no se usan
/// en paralelo, y una publicacion tarda milisegundos.
/// </summary>
public sealed class BusRabbitMq(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    ILogger<BusRabbitMq> log) : IEventBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _uso = new(1, 1);
    private IChannel? _canal;

    public async Task PublicarAsync<T>(MensajeIntegracion<T> mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        using var tope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tope.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutSegundos));

        await _uso.WaitAsync(tope.Token);
        try
        {
            var canal = await CanalAsync(tope.Token);

            var propiedades = new BasicProperties
            {
                MessageId = mensaje.MensajeId.ToString(),
                Type = mensaje.Tipo,
                ContentType = "application/json",
                ContentEncoding = Encoding.UTF8.WebName,
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(mensaje.OcurridoEn.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>
                {
                    ["version"] = mensaje.Version,
                    [CorrelacionMiddleware.NombreCabecera] = mensaje.CorrelationId
                }
            };

            await canal.BasicPublishAsync(opciones.Exchange, mensaje.Tipo, mandatory: false,
                basicProperties: propiedades, body: SerializacionMensajes.Serializar(mensaje),
                cancellationToken: tope.Token);

            log.LogInformation(
                "Publicado {Tipo} v{Version} mensaje {MensajeId}", mensaje.Tipo, mensaje.Version, mensaje.MensajeId);
        }
        catch
        {
            // Un canal que fallo puede quedar en un estado inutil: se descarta y el proximo se abre nuevo.
            await DescartarCanalAsync();
            throw;
        }
        finally
        {
            _uso.Release();
        }
    }

    private async Task<IChannel> CanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true } abierto)
            return abierto;

        var conectada = await conexion.ObtenerAsync("RepMatch publicador", ct);
        _canal = await conectada.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await TopologiaRabbitMq.DeclararExchangesAsync(_canal, opciones, ct);
        return _canal;
    }

    private async Task DescartarCanalAsync()
    {
        if (_canal is null)
            return;

        try
        {
            await _canal.DisposeAsync();
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "No se pudo cerrar el canal de publicacion");
        }

        _canal = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DescartarCanalAsync();
        _uso.Dispose();
    }
}
