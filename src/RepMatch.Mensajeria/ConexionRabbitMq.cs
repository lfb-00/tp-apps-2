using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Mensajeria;

/// <summary>El broker no esta disponible. Lo lanza la conexion al instante mientras dura la pausa
/// despues de una falla, para que publicar no haga esperar a nadie.</summary>
public sealed class BrokerNoDisponibleException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <summary>
/// Una sola conexion TCP al broker por proceso, compartida (RabbitMQ recomienda una conexion y
/// varios canales). Se abre la primera vez que se la necesita, no al arrancar: la Web tiene que
/// levantar aunque RabbitMQ no este.
///
/// Si no puede conectar, deja de intentar durante <see cref="OpcionesMensajeria.PausaTrasFallaSegundos"/>:
/// sin esa pausa, con el broker caido cada "Compartir" o "Ir a la tienda" esperaria el timeout de
/// conexion antes de responder. Una vez conectada, la reconexion la maneja el cliente de RabbitMQ
/// (recuperacion automatica de conexion y topologia).
/// </summary>
public sealed class ConexionRabbitMq(OpcionesMensajeria opciones, ILogger<ConexionRabbitMq> log) : IAsyncDisposable
{
    private readonly SemaphoreSlim _apertura = new(1, 1);
    private IConnection? _conexion;
    private DateTimeOffset _noAntesDe = DateTimeOffset.MinValue;

    public async Task<IConnection> ObtenerAsync(string nombreCliente, CancellationToken ct)
    {
        if (_conexion is { IsOpen: true } abierta)
            return abierta;

        if (DateTimeOffset.UtcNow < _noAntesDe)
            throw new BrokerNoDisponibleException(
                $"RabbitMQ no respondio hace poco; se reintenta despues de {_noAntesDe:HH:mm:ss} UTC.");

        await _apertura.WaitAsync(ct);
        try
        {
            if (_conexion is { IsOpen: true } yaAbierta)
                return yaAbierta;

            var fabrica = new ConnectionFactory
            {
                HostName = opciones.Host,
                Port = opciones.Puerto,
                UserName = opciones.Usuario,
                Password = opciones.Contrasena,
                VirtualHost = opciones.VirtualHost,
                ClientProvidedName = nombreCliente,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos)
            };

            try
            {
                _conexion = await fabrica.CreateConnectionAsync(ct);
                log.LogInformation("Conectado a RabbitMQ en {Host}:{Puerto} como {Cliente}",
                    opciones.Host, opciones.Puerto, nombreCliente);
                return _conexion;
            }
            catch (Exception ex)
            {
                // Toda falla activa la pausa, tambien un corte por tiempo: quien publica cancela con
                // su propio tope, y si eso no contara, cada publicacion volveria a esperar entero.
                _noAntesDe = DateTimeOffset.UtcNow.AddSeconds(opciones.PausaTrasFallaSegundos);
                log.LogWarning("RabbitMQ no disponible en {Host}:{Puerto}; no se reintenta hasta {NoAntesDe:HH:mm:ss} UTC",
                    opciones.Host, opciones.Puerto, _noAntesDe);

                if (ex is OperationCanceledException && ct.IsCancellationRequested)
                    throw;
                throw new BrokerNoDisponibleException(
                    $"No se pudo conectar a RabbitMQ en {opciones.Host}:{opciones.Puerto}.", ex);
            }
        }
        finally
        {
            _apertura.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
            await _conexion.DisposeAsync();
        _apertura.Dispose();
    }
}
