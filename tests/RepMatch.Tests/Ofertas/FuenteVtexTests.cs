using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static HttpResponseMessage Json(string json, HttpStatusCode estado = HttpStatusCode.PartialContent) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static FuenteVtex Crear(TiendaFalsa tienda, OpcionesTiendaVtex? opciones = null) =>
        new(opciones ?? Easy, new Fabrica(tienda), new MemoryCache(new MemoryCacheOptions()), NullLogger<FuenteVtex>.Instance);

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

        await fuente.BuscarAsync(Bateria);
        await fuente.BuscarAsync(Bateria);

        Assert.Single(tienda.Pedidos);
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
        Assert.Equal(CoincidenciasOferta.PorCodigo, Assert.Single(ofertas, o => o.Ean == "7797535000843").Coincidencia);
    }

    [Fact]
    public async Task Sin_resultados_devuelve_una_lista_vacia()
    {
        var tienda = new TiendaFalsa(_ => Json("[]", HttpStatusCode.OK));

        Assert.Empty(await Crear(tienda).BuscarAsync(Bateria));
    }
}
