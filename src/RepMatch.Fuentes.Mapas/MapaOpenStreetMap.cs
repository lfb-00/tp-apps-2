using System.Net;
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

namespace RepMatch.Fuentes.Mapas;

/// <summary>
/// Adapter de las APIs publicas de OpenStreetMap. Dos usos distintos, con politicas distintas:
/// <list type="bullet">
/// <item><b>Descargar las casas de repuestos de Argentina</b> (Overpass, <c>shop=car_parts</c>):
/// un pedido grande que hace el worker una vez por semana, con timeout largo y sin reintentos
/// propios; si Overpass esta saturado, el worker vuelve a probar mas tarde y queda la copia anterior.
/// Se probo primero consultar Overpass en cada busqueda: sus servidores publicos respondian "at
/// capacity" y la persona se quedaba sin resultados.</item>
/// <item><b>Ubicar una zona escrita</b> (Nominatim): mientras la persona espera, con timeout corto,
/// un reintento, circuito con Polly y cache de un dia, como pide su politica de uso (un pedido por
/// segundo como maximo y un User-Agent propio). Solo viaja el texto de la zona, nunca la ubicacion
/// del navegador: esa se usa contra la base propia y no sale de RepMatch.</item>
/// </list>
/// </summary>
public sealed class MapaOpenStreetMap : IMapaCasasRepuestos
{
    public const string ClienteOverpass = "osm-overpass";
    public const string ClienteNominatim = "osm-nominatim";

    /// <summary>
    /// Todas las casas de repuestos dentro del limite de Argentina, con el centro de los locales
    /// cargados como contorno. Pide la geometria del pais una vez y filtra por tipo de comercio.
    /// </summary>
    public static string ConsultaArgentina(int timeoutSegundos) =>
        $"[out:json][timeout:{timeoutSegundos}];"
        + "area[\"ISO3166-1\"=\"AR\"][admin_level=2]->.ar;"
        + "nwr[\"shop\"=\"car_parts\"](area.ar);"
        + "out center tags;";

    private readonly OpcionesMapas _opciones;
    private readonly IHttpClientFactory _fabrica;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MapaOpenStreetMap> _log;
    private readonly SemaphoreSlim _unoPorVez = new(1, 1);
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencia;

    public MapaOpenStreetMap(
        OpcionesMapas opciones, IHttpClientFactory fabrica, IDistributedCache cache, ILogger<MapaOpenStreetMap> log)
    {
        _opciones = opciones;
        _fabrica = fabrica;
        _cache = cache;
        _log = log;
        _resiliencia = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = FallosTransitorios(),
                OnOpened = args =>
                {
                    _log.LogWarning(args.Outcome.Exception,
                        "Circuito abierto para Nominatim durante {Segundos} segundos", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromSeconds(1),
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

    public bool EstaDisponible => true;

    public async Task<IReadOnlyList<CasaRepuestosDto>> DescargarCasasAsync(CancellationToken ct = default)
    {
        using var tope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tope.CancelAfter(TimeSpan.FromSeconds(_opciones.TimeoutDescargaSegundos));

        var consulta = ConsultaArgentina(_opciones.TimeoutDescargaSegundos);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "interpreter")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", consulta)])
        };

        try
        {
            using var respuesta = await _fabrica.CreateClient(ClienteOverpass).SendAsync(pedido, tope.Token);
            if (!respuesta.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Overpass respondio {(int)respuesta.StatusCode}.", null, respuesta.StatusCode);

            var json = await respuesta.Content.ReadAsStringAsync(tope.Token);
            return LectorOpenStreetMap.LeerCasas(json);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException($"Overpass no respondio en {_opciones.TimeoutDescargaSegundos} segundos.");
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("Overpass respondio algo que no se pudo leer.", ex);
        }
    }

    /// <summary>
    /// Las casas de repuestos de Argentina descargadas de Overpass al armar esta version
    /// (<c>Datos/casas-argentina.json</c>, ODbL). Solo se usa si la base esta vacia y Overpass no
    /// responde; la proxima descarga que funcione la reemplaza.
    /// </summary>
    public (IReadOnlyList<CasaRepuestosDto> Casas, DateTimeOffset Fecha)? CopiaIncluida()
    {
        using var recurso = typeof(MapaOpenStreetMap).Assembly.GetManifestResourceStream("RepMatch.Fuentes.Mapas.casas-argentina.json");
        if (recurso is null)
            return null;
        using var lector = new StreamReader(recurso, Encoding.UTF8);
        var json = lector.ReadToEnd();
        return (LectorOpenStreetMap.LeerCasas(json), LectorOpenStreetMap.LeerFecha(json) ?? DateTimeOffset.MinValue);
    }

    public async Task<UbicacionDto?> UbicarAsync(string zona, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(zona);
        var limpia = string.Join(' ', zona.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (limpia.Length == 0)
            return null;

        var claveCache = $"mapas|zona|{limpia.ToLowerInvariant()}";
        if (await LeerCacheAsync(claveCache, ct) is { } guardado)
            return LectorOpenStreetMap.LeerUbicacion(guardado);

        var ruta = $"search?q={Uri.EscapeDataString(limpia)}&countrycodes=ar&format=jsonv2&limit=1";
        await _unoPorVez.WaitAsync(ct);
        try
        {
            var http = _fabrica.CreateClient(ClienteNominatim);
            using var respuesta = await _resiliencia.ExecuteAsync(
                async token => await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, ruta), token), ct);
            if (!respuesta.IsSuccessStatusCode)
                throw new HttpRequestException($"Nominatim respondio {(int)respuesta.StatusCode}.", null, respuesta.StatusCode);

            var json = await respuesta.Content.ReadAsStringAsync(ct);
            var ubicacion = LectorOpenStreetMap.LeerUbicacion(json);
            await GuardarCacheAsync(claveCache, json, ct);
            return ubicacion;
        }
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
        {
            throw new HttpRequestException("Nominatim no respondio a tiempo.", ex);
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("Nominatim respondio algo que no se pudo leer.", ex);
        }
        finally
        {
            _unoPorVez.Release();
        }
    }

    /// <summary>Una cache caida no impide ubicar la zona: se registra y se sigue por red.</summary>
    private async Task<string?> LeerCacheAsync(string clave, CancellationToken ct)
    {
        if (_opciones.CacheHoras == 0)
            return null;
        try
        {
            var bytes = await _cache.GetAsync(clave, ct);
            return bytes is null ? null : Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "No se pudo leer la cache del mapa ({Clave})", clave);
            return null;
        }
    }

    private async Task GuardarCacheAsync(string clave, string json, CancellationToken ct)
    {
        if (_opciones.CacheHoras == 0)
            return;
        try
        {
            await _cache.SetAsync(clave, Encoding.UTF8.GetBytes(json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_opciones.CacheHoras) }, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "No se pudo guardar en la cache del mapa ({Clave})", clave);
        }
    }

    private static PredicateBuilder<HttpResponseMessage> FallosTransitorios() =>
        new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(r => r.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout
                               || (int)r.StatusCode >= 500);
}
