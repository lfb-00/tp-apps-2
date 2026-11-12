using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Fuentes.Vtex;

namespace RepMatch.Tests.Ofertas;

/// <summary>El adapter con HTTP simulado: 206 como respuesta valida, errores, cache y orden de terminos.</summary>
public class FuenteVtexTests
{
    private static readonly OpcionesTiendaVtex Easy = new()
    {
        Nombre = "Easy",
        UrlBase = "https://www.easy.com.ar",
        PrefijosCategoria = ["/Automotor/"]
    };

    private static readonly ConsultaOfertasDto Bateria = new()
    {
        CodigoRepuesto = "BATE-12V-60AH",
        Nombre = "Bateria 12V 60Ah"
    };

    /// <summary>Responde lo que diga la funcion y anota cada pedido.</summary>
    private sealed class TiendaFalsa(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Uri> Pedidos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            Pedidos.Add(peticion.RequestUri!);
            return Task.FromResult(responder(peticion));
        }
    }

    private sealed class Fabrica(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(manejador, disposeHandler: false) { BaseAddress = new Uri(Easy.UrlBase + "/") };
    }

    private sealed class TiendaLenta : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode estado = HttpStatusCode.PartialContent) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static FuenteVtex Crear(HttpMessageHandler tienda, OpcionesTiendaVtex? opciones = null)
    {
        var cache = new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        return new FuenteVtex(opciones ?? Easy, new Fabrica(tienda), cache, NullLogger<FuenteVtex>.Instance);
    }

    [Fact]
    public async Task Acepta_206_y_pide_la_primera_pagina_del_buscador_de_catalogo()
    {
        var tienda = new TiendaFalsa(_ => Json(LectorVtexTests.Grabada("easy-bateria.json")));

        var ofertas = await Crear(tienda).BuscarAsync(Bateria);

        Assert.Equal(3, ofertas.Count);
        var pedido = Assert.Single(tienda.Pedidos);
        Assert.Equal("/api/catalog_system/pub/products/search", pedido.AbsolutePath);
        Assert.Contains("ft=Bateria%2012V%2060Ah", pedido.Query);
        Assert.Contains("_from=0&_to=9", pedido.Query);
    }

    [Fact]
    public async Task Un_error_de_la_tienda_se_propaga_como_HttpRequestException()
    {
        var tienda = new TiendaFalsa(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => Crear(tienda).BuscarAsync(Bateria));
    }

    [Fact]
    public async Task Reutiliza_la_respuesta_de_un_termino_ya_consultado()
    {
        var tienda = new TiendaFalsa(_ => Json(LectorVtexTests.Grabada("easy-bateria.json")));
        var fuente = Crear(tienda);

        var primera = await fuente.BuscarAsync(Bateria);
        var segunda = await fuente.BuscarAsync(Bateria with { Nombre = "BATERIA   12V 60AH" });

        Assert.Single(tienda.Pedidos);
        Assert.All(segunda, oferta => Assert.Equal(primera.Single(o => o.Url == oferta.Url).CapturadaEn, oferta.CapturadaEn));
    }

    [Fact]
    public async Task Reintenta_y_abre_el_circuito_despues_de_tres_consultas_fallidas()
    {
        var tienda = new TiendaFalsa(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var fuente = Crear(tienda, new OpcionesTiendaVtex
        {
            Nombre = Easy.Nombre,
            UrlBase = Easy.UrlBase,
            CacheMinutos = 0
        });

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<HttpRequestException>(() => fuente.BuscarAsync(Bateria));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => fuente.BuscarAsync(Bateria));

        Assert.IsType<BrokenCircuitException>(ex.InnerException);
        Assert.Equal(9, tienda.Pedidos.Count);
    }

    [Fact]
    public async Task El_timeout_de_la_tienda_se_traduce_a_HttpRequestException()
    {
        var fuente = Crear(new TiendaLenta(), new OpcionesTiendaVtex
        {
            Nombre = Easy.Nombre,
            UrlBase = Easy.UrlBase,
            TimeoutSegundos = 1,
            CacheMinutos = 0
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => fuente.BuscarAsync(Bateria));

        Assert.IsType<TimeoutRejectedException>(ex.InnerException);
    }

    [Fact]
    public async Task Prueba_primero_los_codigos_y_no_duplica_lo_que_encuentra_por_nombre()
    {
        var tienda = new TiendaFalsa(_ => Json(LectorVtexTests.Grabada("easy-bateria.json")));
        var conEan = Bateria with { CodigosEquivalentes = ["7797535000843"] };

        var ofertas = await Crear(tienda).BuscarAsync(conEan);

        Assert.Equal(2, tienda.Pedidos.Count);
        Assert.Contains("ft=7797535000843", tienda.Pedidos[0].Query);
        Assert.Equal(3, ofertas.Count);
        Assert.Equal(CoincidenciasOferta.ExactoPorEan, Assert.Single(ofertas, o => o.Ean == "7797535000843").Coincidencia);
    }

    [Fact]
    public async Task Sin_resultados_devuelve_una_lista_vacia()
    {
        var tienda = new TiendaFalsa(_ => Json("[]", HttpStatusCode.OK));

        Assert.Empty(await Crear(tienda).BuscarAsync(Bateria));
    }
}
