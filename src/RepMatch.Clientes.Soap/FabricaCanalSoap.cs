using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using Microsoft.AspNetCore.Http;
using RepMatch.Clientes.Soap.ServicioSoap;
using RepMatch.Common.Logging;

namespace RepMatch.Clientes.Soap;

/// <summary>
/// La ChannelFactory del servicio SOAP, una sola para toda la app: crearla es caro (arma el
/// binding y el cliente HTTP) y es segura entre hilos; los canales, en cambio, son baratos y se
/// crean por llamada.
///
/// Permite enchufar el HttpMessageHandler que usa WCF por debajo. Con eso se propaga el
/// X-Correlation-Id igual que en el cliente REST, y las pruebas pueden pasar el handler de un
/// TestServer para hablar con el servicio en memoria.
/// </summary>
public sealed class FabricaCanalSoap : IDisposable
{
    private readonly ChannelFactory<ConsultaCompatibilidadChannel> _fabrica;

    public FabricaCanalSoap(string url, TimeSpan timeout, Func<HttpMessageHandler, HttpMessageHandler>? envolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var binding = new BasicHttpBinding
        {
            // El listado completo del catalogo supera los 64 KB que trae el binding por defecto.
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            OpenTimeout = timeout,
            SendTimeout = timeout,
            ReceiveTimeout = timeout,
            CloseTimeout = timeout
        };
        binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
        binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

        _fabrica = new ChannelFactory<ConsultaCompatibilidadChannel>(binding, new EndpointAddress(url));
        if (envolver is not null)
            _fabrica.Endpoint.EndpointBehaviors.Add(new HandlerHttp(envolver));
    }

    public ConsultaCompatibilidadChannel CrearCanal() => _fabrica.CreateChannel();

    public void Dispose()
    {
        if (_fabrica.State == CommunicationState.Faulted)
            _fabrica.Abort();
        else
            _fabrica.Close();
    }

    /// <summary>System.ServiceModel.Http deja reemplazar el handler HTTP con un parametro de binding.</summary>
    private sealed class HandlerHttp(Func<HttpMessageHandler, HttpMessageHandler> envolver) : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection parametros) =>
            parametros.Add(new Func<HttpClientHandler, HttpMessageHandler>(handler => envolver(handler)));

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime cliente) { }
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher dispatcher) { }
        public void Validate(ServiceEndpoint endpoint) { }
    }
}

/// <summary>
/// Copia el X-Correlation-Id de la peticion entrante al mensaje SOAP saliente, como
/// PropagacionCorrelacionHandler en Clientes.Rest. Se repite aca para que los dos adapters no
/// dependan uno del otro: cada uno se puede quitar sin tocar el otro.
/// </summary>
public sealed class CorrelacionSoapHandler(IHttpContextAccessor contexto, HttpMessageHandler interno)
    : DelegatingHandler(interno)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
    {
        var correlationId = contexto.HttpContext?.Items[CorrelacionMiddleware.NombreCabecera] as string;

        if (!string.IsNullOrWhiteSpace(correlationId) && !peticion.Headers.Contains(CorrelacionMiddleware.NombreCabecera))
            peticion.Headers.Add(CorrelacionMiddleware.NombreCabecera, correlationId);

        return base.SendAsync(peticion, ct);
    }
}
