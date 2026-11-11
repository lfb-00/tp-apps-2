using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Fuentes.Vtex;

/// <summary>
/// Adapter de una tienda VTEX. Una sola clase para todas: la tienda sale de la configuracion
/// (<see cref="OpcionesTiendaVtex"/>), asi que sumar otra tienda VTEX no requiere codigo.
///
/// Usa el buscador publico de catalogo de VTEX, la API que la propia tienda usa para su sitio:
/// responde JSON sin credenciales y no es scraping. Por cada pieza consulta primero los codigos
/// equivalentes (precisos, pero las tiendas generalistas casi nunca los publican) y despues el
/// nombre (mas cobertura, menos precision: queda marcado como coincidencia por nombre).
///
/// Es singleton a proposito: comparte entre todas las personas el limite de pedidos simultaneos y
/// la cache de respuestas, que existen para no castigar a una API gratuita.
/// </summary>
public sealed class FuenteVtex(
    OpcionesTiendaVtex tienda,
    IHttpClientFactory fabrica,
    IMemoryCache cache,
    ILogger<FuenteVtex> log) : IFuenteOfertas
{
    /// <summary>Mas codigos casi nunca suman resultados y si suman pedidos.</summary>
    private const int MaximoCodigos = 2;

    private readonly SemaphoreSlim _limite = new(tienda.PedidosSimultaneos, tienda.PedidosSimultaneos);

    public string Nombre => tienda.Nombre;

    /// <summary>Nombre del HttpClient de esta tienda en IHttpClientFactory.</summary>
    public static string NombreCliente(OpcionesTiendaVtex tienda) => $"vtex-{tienda.Nombre.ToLowerInvariant()}";

    public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var porReferencia = new Dictionary<string, OfertaExternaDto>(StringComparer.OrdinalIgnoreCase);

        var terminos = consulta.CodigosEquivalentes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(MaximoCodigos)
            .Select(c => (Termino: c.Trim(), EsCodigo: true))
            .Append((Termino: consulta.Nombre.Trim(), EsCodigo: false));

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

    private async Task<IReadOnlyList<OfertaExternaDto>> BuscarTerminoAsync(
        ConsultaOfertasDto consulta, string termino, bool esCodigo, CancellationToken ct)
    {
        var json = await ObtenerRespuestaAsync(termino, ct);
        return LectorVtex.Leer(json, tienda, consulta, termino, esCodigo);
    }

    /// <summary>
    /// El JSON crudo de la busqueda, cacheado por termino: la cache guarda la respuesta de la tienda,
    /// no la interpretacion, porque el mismo termino ("Bateria 12V 60Ah") sirve a varias piezas.
    /// </summary>
    private async Task<string> ObtenerRespuestaAsync(string termino, CancellationToken ct)
    {
        var clave = $"{NombreCliente(tienda)}|{termino.ToLowerInvariant()}";
        if (tienda.CacheMinutos > 0 && cache.TryGetValue(clave, out string? guardada) && guardada is not null)
            return guardada;

        var ruta = "api/catalog_system/pub/products/search"
                   + $"?ft={Uri.EscapeDataString(termino)}&_from=0&_to={tienda.ResultadosPorTermino - 1}";

        await _limite.WaitAsync(ct);
        try
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await fabrica.CreateClient(NombreCliente(tienda)).GetAsync(ruta, ct);

            // VTEX responde 206 Partial Content cuando hay mas resultados que la pagina pedida: es
            // una respuesta completa de esa pagina, no un error.
            if (respuesta.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
                throw new HttpRequestException(
                    $"{tienda.Nombre} respondio {(int)respuesta.StatusCode} al buscar '{termino}'.", null, respuesta.StatusCode);

            var json = await respuesta.Content.ReadAsStringAsync(ct);
            log.LogInformation(
                "Tienda {Tienda} termino={Termino} estado={Estado} en {Milisegundos} ms",
                tienda.Nombre, termino, (int)respuesta.StatusCode, reloj.ElapsedMilliseconds);

            if (tienda.CacheMinutos > 0)
                cache.Set(clave, json, TimeSpan.FromMinutes(tienda.CacheMinutos));

            return json;
        }
        finally
        {
            _limite.Release();
        }
    }
}
