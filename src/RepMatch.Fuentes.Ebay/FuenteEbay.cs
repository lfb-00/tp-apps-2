using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Fuentes.Ebay;

/// <summary>
/// Adapter de la Browse API de eBay (issue #17): la segunda <see cref="IFuenteOfertas"/>, despues de
/// las tiendas VTEX. <c>ServicioOfertas</c> no cambio para sumarla.
///
/// eBay pide un token de aplicacion (OAuth 2.0, client credentials) que dura unas dos horas: se
/// pide una vez, se guarda en memoria y se renueva poco antes de vencer, o enseguida si eBay lo
/// rechaza. Las busquedas son por numero de fabricante (<see cref="LectorEbay"/>), en USD: el
/// dominio no mezcla monedas y la pantalla muestra estas ofertas aparte de las en pesos.
///
/// Igual que <c>FuenteVtex</c>: singleton (comparte el limite de pedidos y el circuito), Polly por
/// fuente y respuestas en la cache distribuida. Una falla de eBay se informa como
/// <see cref="HttpRequestException"/> y la busqueda sigue con las demas tiendas.
/// </summary>
public sealed class FuenteEbay : IFuenteOfertas
{
    public const string NombreCliente = "ebay";

    private const string Alcance = "https://api.ebay.com/oauth/api_scope";

    /// <summary>Margen antes del vencimiento del token: no se usa uno que vence en mitad del pedido.</summary>
    private static readonly TimeSpan MargenToken = TimeSpan.FromMinutes(5);

    private readonly OpcionesEbay _opciones;
    private readonly IHttpClientFactory _fabrica;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FuenteEbay> _log;
    private readonly TimeProvider _reloj;
    private readonly SemaphoreSlim _limite;
    private readonly SemaphoreSlim _candadoToken = new(1, 1);
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencia;

    private string? _token;
    private DateTimeOffset _tokenVence;

    private sealed record RespuestaCache(string Json, DateTimeOffset CapturadaEn);

    public FuenteEbay(
        OpcionesEbay opciones,
        IHttpClientFactory fabrica,
        IDistributedCache cache,
        ILogger<FuenteEbay> log,
        TimeProvider? reloj = null)
    {
        _opciones = opciones;
        _fabrica = fabrica;
        _cache = cache;
        _log = log;
        _reloj = reloj ?? TimeProvider.System;
        _limite = new SemaphoreSlim(opciones.PedidosSimultaneos, opciones.PedidosSimultaneos);
        _resiliencia = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = FallosTransitorios(),
                OnOpened = args =>
                {
                    _log.LogWarning(args.Outcome.Exception,
                        "Circuito abierto para eBay durante {Segundos} segundos", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = FallosTransitorios(),
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(opciones.TimeoutSegundos))
            .Build();
    }

    public string Nombre => OpcionesEbay.NombreTienda;

    public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var codigos = consulta.CodigosEquivalentes
            .Select(CodigosRepuesto.SinMarca)
            .Where(c => c.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_opciones.MaximoCodigos);

        var porReferencia = new Dictionary<string, OfertaExternaDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var codigo in codigos)
        {
            RespuestaCache respuesta;
            try
            {
                respuesta = await ObtenerRespuestaAsync(codigo, ct);
            }
            catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
            {
                throw new HttpRequestException("eBay no respondió dentro de su política de resiliencia.", ex);
            }

            foreach (var oferta in LectorEbay.Leer(respuesta.Json, consulta, codigo, respuesta.CapturadaEn))
                porReferencia.TryAdd(oferta.Referencia ?? oferta.Url, oferta);
        }

        return [.. porReferencia.Values];
    }

    private async Task<RespuestaCache> ObtenerRespuestaAsync(string codigo, CancellationToken ct)
    {
        var clave = $"{NombreCliente}|{_opciones.Marketplace}|{codigo.ToUpperInvariant()}";
        if (_opciones.CacheMinutos > 0 && await LeerCacheAsync(clave, ct) is { } guardada)
        {
            _log.LogInformation("eBay codigo={Codigo} origen=cache capturada={CapturadaEn}", codigo, guardada.CapturadaEn);
            return guardada;
        }

        var ruta = "buy/browse/v1/item_summary/search"
                   + $"?q={Uri.EscapeDataString(codigo)}&limit={_opciones.ResultadosPorTermino}";

        await _limite.WaitAsync(ct);
        try
        {
            var reloj = Stopwatch.StartNew();
            using var cliente = _fabrica.CreateClient(NombreCliente);
            using var respuesta = await _resiliencia.ExecuteAsync(async token =>
            {
                var recibida = await EnviarBusquedaAsync(cliente, ruta, renovarToken: false, token);
                if (recibida.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // El token vencio antes de lo previsto o eBay lo revoco: uno nuevo, una sola vez.
                    recibida.Dispose();
                    recibida = await EnviarBusquedaAsync(cliente, ruta, renovarToken: true, token);
                }

                if (!recibida.IsSuccessStatusCode)
                {
                    var estado = recibida.StatusCode;
                    recibida.Dispose();
                    throw new HttpRequestException($"eBay respondio {(int)estado} al buscar '{codigo}'.", null, estado);
                }

                return recibida;
            }, ct);

            var resultado = new RespuestaCache(await respuesta.Content.ReadAsStringAsync(ct), _reloj.GetUtcNow());
            _log.LogInformation("eBay codigo={Codigo} estado={Estado} origen=red en {Milisegundos} ms",
                codigo, (int)respuesta.StatusCode, reloj.ElapsedMilliseconds);

            if (_opciones.CacheMinutos > 0)
                await GuardarCacheAsync(clave, resultado, ct);

            return resultado;
        }
        finally
        {
            _limite.Release();
        }
    }

    private async Task<HttpResponseMessage> EnviarBusquedaAsync(
        HttpClient cliente, string ruta, bool renovarToken, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await ObtenerTokenAsync(cliente, renovarToken, ct));
        peticion.Headers.Add("X-EBAY-C-MARKETPLACE-ID", _opciones.Marketplace);
        return await cliente.SendAsync(peticion, ct);
    }

    /// <summary>Token de aplicacion (client credentials). Un candado evita que varias busquedas
    /// simultaneas pidan cada una el suyo.</summary>
    private async Task<string> ObtenerTokenAsync(HttpClient cliente, bool renovar, CancellationToken ct)
    {
        if (!renovar && _token is not null && _reloj.GetUtcNow() < _tokenVence)
            return _token;

        await _candadoToken.WaitAsync(ct);
        try
        {
            if (!renovar && _token is not null && _reloj.GetUtcNow() < _tokenVence)
                return _token;

            using var peticion = new HttpRequestMessage(HttpMethod.Post, "identity/v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = Alcance
                })
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opciones.ClientId}:{_opciones.ClientSecret}")));

            using var respuesta = await cliente.SendAsync(peticion, ct);
            if (!respuesta.IsSuccessStatusCode)
                // Sin el cuerpo: puede repetir datos de la aplicacion. El estado alcanza para diagnosticar.
                throw new HttpRequestException(
                    $"eBay rechazo las credenciales al pedir el token ({(int)respuesta.StatusCode}).", null, respuesta.StatusCode);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            var token = json.RootElement.GetProperty("access_token").GetString()
                        ?? throw new HttpRequestException("eBay respondio un token vacio.");
            var segundos = json.RootElement.TryGetProperty("expires_in", out var vence) ? vence.GetInt32() : 7200;

            _token = token;
            _tokenVence = _reloj.GetUtcNow().AddSeconds(segundos) - MargenToken;
            _log.LogInformation("eBay: token de aplicacion obtenido, vence en {Segundos} s", segundos);
            return token;
        }
        finally
        {
            _candadoToken.Release();
        }
    }

    private async Task<RespuestaCache?> LeerCacheAsync(string clave, CancellationToken ct)
    {
        try
        {
            var bytes = await _cache.GetAsync(clave, ct);
            if (bytes is null)
                return null;
            var guardada = JsonSerializer.Deserialize<RespuestaCache>(bytes);
            return guardada is { CapturadaEn: var capturada } && capturada != default ? guardada : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "No se pudo leer la cache de eBay; se consulta la red");
            return null;
        }
    }

    private async Task GuardarCacheAsync(string clave, RespuestaCache resultado, CancellationToken ct)
    {
        try
        {
            await _cache.SetAsync(clave, JsonSerializer.SerializeToUtf8Bytes(resultado),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_opciones.CacheMinutos)
                }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "No se pudo guardar en cache la respuesta de eBay");
        }
    }

    /// <summary>Se reintenta lo transitorio: red, timeout, 429 y 5xx. Un 4xx de la API (por ejemplo,
    /// sin permiso para la Browse API) no mejora reintentando.</summary>
    private static PredicateBuilder<HttpResponseMessage> FallosTransitorios() =>
        new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>(ex =>
                ex.StatusCode is null or HttpStatusCode.TooManyRequests || (int?)ex.StatusCode >= 500)
            .Handle<TimeoutRejectedException>();
}
