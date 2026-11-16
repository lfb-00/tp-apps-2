using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using StackExchange.Redis;

namespace RepMatch.Fuentes.Vtex;

/// <summary>
/// Adapter de una tienda VTEX. Una sola clase para todas: la tienda sale de la configuracion
/// (<see cref="OpcionesTiendaVtex"/>), asi que sumar otra tienda VTEX no requiere codigo.
///
/// Usa el buscador publico de catalogo de VTEX, la API que la propia tienda usa para su sitio:
/// responde JSON sin credenciales y no es scraping. Por cada pieza consulta primero los codigos
/// equivalentes, sin la marca ni espacios (<see cref="CodigosRepuesto.ParaBuscar"/>), y despues el
/// nombre. Lo que vuelve es exacto si la publicacion menciona uno de los codigos de la pieza, se
/// haya encontrado por codigo o por nombre; si no, queda como coincidencia por nombre.
///
/// Es singleton a proposito: comparte entre todas las personas el limite de pedidos simultaneos y
/// el circuito de esta tienda. La cache distribuida comparte las respuestas entre instancias de Web.
/// </summary>
public sealed class FuenteVtex : IFuenteOfertas
{
    /// <summary>Mas codigos casi nunca suman resultados y si suman pedidos.</summary>
    private const int MaximoCodigos = 2;

    private readonly OpcionesTiendaVtex _tienda;
    private readonly IHttpClientFactory _fabrica;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FuenteVtex> _log;
    private readonly SemaphoreSlim _limite;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencia;

    private sealed record RespuestaCache(string Json, DateTimeOffset CapturadaEn);

    public FuenteVtex(
        OpcionesTiendaVtex tienda,
        IHttpClientFactory fabrica,
        IDistributedCache cache,
        ILogger<FuenteVtex> log)
    {
        _tienda = tienda;
        _fabrica = fabrica;
        _cache = cache;
        _log = log;
        _limite = new SemaphoreSlim(tienda.PedidosSimultaneos, tienda.PedidosSimultaneos);
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
                    _log.LogWarning(
                        args.Outcome.Exception,
                        "Circuito abierto para la tienda {Tienda} durante {Segundos} segundos",
                        _tienda.Nombre,
                        args.BreakDuration.TotalSeconds);
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
                    _log.LogWarning(
                        args.Outcome.Exception,
                        "Reintento {Intento} para la tienda {Tienda}",
                        args.AttemptNumber + 1,
                        _tienda.Nombre);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(tienda.TimeoutSegundos))
            .Build();
    }

    public string Nombre => _tienda.Nombre;

    /// <summary>Nombre del HttpClient de esta tienda en IHttpClientFactory.</summary>
    public static string NombreCliente(OpcionesTiendaVtex tienda) => $"vtex-{tienda.Nombre.ToLowerInvariant()}";

    public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var porReferencia = new Dictionary<string, OfertaExternaDto>(StringComparer.OrdinalIgnoreCase);

        var terminos = consulta.CodigosEquivalentes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(MaximoCodigos)
            .Select(c => (Termino: CodigosRepuesto.ParaBuscar(c), EsCodigo: true))
            .Append((Termino: TerminoDeNombre(consulta.Nombre), EsCodigo: false));

        foreach (var (termino, esCodigo) in terminos)
        {
            foreach (var oferta in await BuscarTerminoAsync(consulta, termino, esCodigo, ct))
            {
                // La misma publicacion puede salir por codigo y por nombre: gana la mas precisa.
                var clave = oferta.Referencia ?? oferta.Url;
                if (!porReferencia.ContainsKey(clave))
                    porReferencia[clave] = oferta;
            }
        }

        return [.. porReferencia.Values];
    }

    /// <summary>
    /// La pagina de busqueda del propio sitio VTEX (<c>/{termino}?_q={termino}&amp;map=ft</c>), solo
    /// para tiendas que venden repuestos. El termino es el tipo de pieza y el modelo ("bujías Gol"):
    /// el buscador de VTEX ordena mal con palabras de relleno, y "Juego de bujías Gol" trae discos
    /// de freno.
    /// </summary>
    public string? UrlParaBuscar(ConsultaOfertasDto consulta, VehiculoDto? vehiculo)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        if (!_tienda.VendeRepuestos)
            return null;

        var termino = TerminoParaSugerir(consulta.Nombre, vehiculo?.Modelo);
        var escapado = Uri.EscapeDataString(termino);
        return $"{_tienda.UrlBase.TrimEnd('/')}/{escapado}?_q={escapado}&map=ft";
    }

    /// <summary>"Juego de bujías (1.6 8V)" y Gol: "bujías Gol".</summary>
    public static string TerminoParaSugerir(string nombre, string? modelo)
    {
        var palabras = TerminoDeNombre(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (palabras.Count > 2 && palabras[0] is "Juego" or "Kit" or "juego" or "kit"
            && palabras[1].Equals("de", StringComparison.OrdinalIgnoreCase))
            palabras.RemoveRange(0, 2);
        palabras.RemoveAll(p => p.Equals("de", StringComparison.OrdinalIgnoreCase)
                                || p.Equals("del", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(modelo))
            palabras.Add(modelo.Trim());
        return string.Join(' ', palabras);
    }

    /// <summary>
    /// El nombre sin lo que va entre parentesis: el buscador de VTEX responde 400 a
    /// "Filtro de aceite (cartucho)" o "Discos de freno delanteros (par)", en Easy y en Autodo.
    /// Ese 400 contaba como falla de la tienda y, a la tercera, el circuito la cortaba 30
    /// segundos para todas las piezas siguientes. Lo de los parentesis aclara la pieza para la
    /// persona; para buscar por nombre alcanza con el resto.
    /// </summary>
    public static string TerminoDeNombre(string nombre)
    {
        ArgumentNullException.ThrowIfNull(nombre);
        var sinAclaracion = Regex.Replace(nombre, @"\([^)]*\)?", " ");
        return string.Join(' ', sinAclaracion.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<IReadOnlyList<OfertaExternaDto>> BuscarTerminoAsync(
        ConsultaOfertasDto consulta, string termino, bool esCodigo, CancellationToken ct)
    {
        try
        {
            var respuesta = await ObtenerRespuestaAsync(termino, ct);
            return LectorVtex.Leer(respuesta.Json, _tienda, consulta, termino, esCodigo, respuesta.CapturadaEn);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
        {
            throw new HttpRequestException(
                $"La tienda {_tienda.Nombre} no respondió dentro de su política de resiliencia.", ex);
        }
    }

    /// <summary>
    /// El JSON crudo de la busqueda, cacheado por fuente y termino normalizado. Se guarda la hora
    /// original de captura junto con la respuesta para que un cache hit no rejuvenezca los precios.
    /// </summary>
    private async Task<RespuestaCache> ObtenerRespuestaAsync(string termino, CancellationToken ct)
    {
        var clave = $"{NombreCliente(_tienda)}|{NormalizarTermino(termino)}";
        if (_tienda.CacheMinutos > 0)
        {
            try
            {
                var bytes = await _cache.GetAsync(clave, ct);
                if (bytes is not null)
                {
                    try
                    {
                        var guardada = JsonSerializer.Deserialize<RespuestaCache>(bytes);
                        if (guardada is not null && guardada.CapturadaEn != default)
                        {
                            _log.LogInformation(
                                "Tienda {Tienda} termino={Termino} origen=cache capturada={CapturadaEn}",
                                _tienda.Nombre, termino, guardada.CapturadaEn);
                            return guardada;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _log.LogWarning(ex, "Cache invalida para la tienda {Tienda} y termino {Termino}", _tienda.Nombre, termino);
                    }

                    await _cache.RemoveAsync(clave, ct);
                }
            }
            catch (Exception ex) when (FalloDeCache(ex, ct))
            {
                _log.LogWarning(ex, "No se pudo leer la cache de la tienda {Tienda}; se consulta la red", _tienda.Nombre);
            }
        }

        var ruta = "api/catalog_system/pub/products/search"
                   + $"?ft={Uri.EscapeDataString(termino)}&_from=0&_to={_tienda.ResultadosPorTermino - 1}";

        await _limite.WaitAsync(ct);
        try
        {
            var reloj = Stopwatch.StartNew();
            using var cliente = _fabrica.CreateClient(NombreCliente(_tienda));
            using var respuesta = await _resiliencia.ExecuteAsync(async token =>
            {
                var recibida = await cliente.GetAsync(ruta, token);

                // VTEX responde 206 Partial Content cuando hay mas resultados que la pagina pedida:
                // es una respuesta completa de esa pagina, no un error.
                if (recibida.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
                {
                    var estado = recibida.StatusCode;
                    recibida.Dispose();
                    throw new HttpRequestException(
                        $"{_tienda.Nombre} respondio {(int)estado} al buscar '{termino}'.", null, estado);
                }

                return recibida;
            }, ct);

            var json = await respuesta.Content.ReadAsStringAsync(ct);
            var capturadaEn = DateTimeOffset.UtcNow;
            var resultado = new RespuestaCache(json, capturadaEn);
            _log.LogInformation(
                "Tienda {Tienda} termino={Termino} estado={Estado} origen=red en {Milisegundos} ms",
                _tienda.Nombre, termino, (int)respuesta.StatusCode, reloj.ElapsedMilliseconds);

            if (_tienda.CacheMinutos > 0)
            {
                try
                {
                    await _cache.SetAsync(
                        clave,
                        JsonSerializer.SerializeToUtf8Bytes(resultado),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_tienda.CacheMinutos)
                        },
                        ct);
                }
                catch (Exception ex) when (FalloDeCache(ex, ct))
                {
                    _log.LogWarning(ex, "No se pudo guardar en cache la respuesta de {Tienda}", _tienda.Nombre);
                }
            }

            return resultado;
        }
        finally
        {
            _limite.Release();
        }
    }

    private static PredicateBuilder<HttpResponseMessage> FallosTransitorios() =>
        new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>();

    private static bool FalloDeCache(Exception ex, CancellationToken ct) =>
        !ct.IsCancellationRequested && ex is RedisException or TimeoutException;

    private static string NormalizarTermino(string termino)
    {
        var normalizado = new StringBuilder(termino.Length);
        foreach (var caracter in termino.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
                continue;

            normalizado.Append(char.IsLetterOrDigit(caracter) ? char.ToLowerInvariant(caracter) : ' ');
        }

        return string.Join(' ', normalizado.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
