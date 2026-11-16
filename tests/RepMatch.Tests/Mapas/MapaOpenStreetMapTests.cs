using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Fuentes.Mapas;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Mapas;

/// <summary>
/// Las casas de repuestos cercanas contra respuestas reales grabadas de OpenStreetMap (Overpass
/// alrededor del centro de Buenos Aires y Nominatim para "Palermo", noviembre de 2026), sin red.
/// El worker copia las casas a la base; la busqueda lee la base y solo una zona escrita sale al mapa.
/// </summary>
public class MapaOpenStreetMapTests
{
    private static readonly UbicacionDto Obelisco = new() { Latitud = -34.6037, Longitud = -58.3816 };
    private readonly string nombreBase = Guid.NewGuid().ToString();

    private static string Grabada(string nombre) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "mapas", nombre));

    private sealed class OsmFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(Uri Url, string Cuerpo)> Pedidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            var cuerpo = pedido.Content is null ? "" : await pedido.Content.ReadAsStringAsync(ct);
            Pedidos.Add((pedido.RequestUri!, cuerpo));
            return responder(pedido);
        }
    }

    private sealed class Fabrica(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false)
        {
            BaseAddress = new Uri(name == MapaOpenStreetMap.ClienteOverpass ? "https://overpass.test/api/" : "https://nominatim.test/")
        };
    }

    private static MapaOpenStreetMap Mapa(HttpMessageHandler osm)
    {
        var cache = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        return new MapaOpenStreetMap(new OpcionesMapas(), new Fabrica(osm), cache, NullLogger<MapaOpenStreetMap>.Instance);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode estado = HttpStatusCode.OK) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private RepMatchDbContext Contexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static ServicioImportacionCasas Importacion(RepMatchDbContext contexto, IMapaCasasRepuestos mapa, TimeProvider? reloj = null) =>
        new(mapa, new CasaRepuestosRepository(contexto), new UnitOfWork(contexto), new OpcionesMapas(),
            NullLogger<ServicioImportacionCasas>.Instance, reloj);

    private static ServicioCasasCercanas Cercanas(RepMatchDbContext contexto, IMapaCasasRepuestos mapa, int maximo = 10) =>
        new(mapa, new CasaRepuestosRepository(contexto), new OpcionesMapas { Maximo = maximo },
            NullLogger<ServicioCasasCercanas>.Instance);

    private async Task ImportarGrabadasAsync()
    {
        await using var contexto = Contexto();
        var resultado = await Importacion(contexto, Mapa(new OsmFalso(_ => Json(Grabada("overpass-centro.json"))))).ImportarAsync();
        Assert.True(resultado.EsExitoso);
    }

    // ── Lo que se lee de OpenStreetMap ───────────────────────────────────────────────────────────

    [Fact]
    public void Lee_los_comercios_con_nombre_puntos_y_locales()
    {
        var casas = LectorOpenStreetMap.LeerCasas(Grabada("overpass-centro.json"), Obelisco);

        Assert.NotEmpty(casas);
        Assert.All(casas, c => Assert.False(string.IsNullOrWhiteSpace(c.Nombre)));
        Assert.Equal(casas.OrderBy(c => c.DistanciaMetros).Select(c => c.Nombre), casas.Select(c => c.Nombre));
        // Hay locales cargados como contorno (way): su punto es el centro que manda Overpass.
        Assert.Contains(casas, c => c.IdMapa.StartsWith("way/", StringComparison.Ordinal));
        Assert.Contains(casas, c => c.IdMapa.StartsWith("node/", StringComparison.Ordinal));
        var primera = casas[0];
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"https://www.google.com/maps/search/?api=1&query={primera.Latitud:0.######},{primera.Longitud:0.######}"),
            primera.UrlMapa);
    }

    [Fact]
    public void Un_comercio_sin_nombre_no_se_guarda_y_la_direccion_falta_si_nadie_la_cargo()
    {
        const string json = """
            {"elements": [
              {"type": "node", "id": 1, "lat": -34.6, "lon": -58.38, "tags": {"shop": "car_parts"}},
              {"type": "node", "id": 2, "lat": -34.601, "lon": -58.38, "tags": {"shop": "car_parts", "name": "Repuestos Sur"}},
              {"type": "node", "id": 3, "lat": -34.61, "lon": -58.38, "tags": {"shop": "car_parts", "name": "Casa Norte",
                "addr:street": "Avenida Belgrano", "addr:housenumber": "1234", "phone": "+54 11 4444-5555"}}
            ]}
            """;

        var casas = LectorOpenStreetMap.LeerCasas(json, new UbicacionDto { Latitud = -34.6, Longitud = -58.38 });

        Assert.Equal(["Repuestos Sur", "Casa Norte"], casas.Select(c => c.Nombre));
        Assert.Null(casas[0].Direccion);
        Assert.Equal("Avenida Belgrano 1234", casas[1].Direccion);
        Assert.Equal("+54 11 4444-5555", casas[1].Telefono);
        Assert.InRange(casas[1].DistanciaMetros, 1050, 1180);   // 0,01 grados de latitud: ~1,1 km
    }

    [Fact]
    public void Ubica_una_zona_con_el_primer_resultado_de_Nominatim()
    {
        var palermo = LectorOpenStreetMap.LeerUbicacion(Grabada("nominatim-palermo.json"));

        Assert.NotNull(palermo);
        Assert.InRange(palermo.Latitud, -34.62, -34.55);
        Assert.StartsWith("Palermo", palermo.Descripcion);
        Assert.Null(LectorOpenStreetMap.LeerUbicacion("[]"));
    }

    [Fact]
    public async Task La_descarga_pide_las_casas_de_repuestos_de_toda_Argentina()
    {
        var osm = new OsmFalso(_ => Json(Grabada("overpass-centro.json")));

        var casas = await Mapa(osm).DescargarCasasAsync();

        Assert.NotEmpty(casas);
        var (url, cuerpo) = Assert.Single(osm.Pedidos);
        Assert.Equal("https://overpass.test/api/interpreter", url.ToString());
        var consulta = Uri.UnescapeDataString(cuerpo.Replace('+', ' '));
        Assert.Contains("area[\"ISO3166-1\"=\"AR\"]", consulta);
        Assert.Contains("nwr[\"shop\"=\"car_parts\"](area.ar)", consulta);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]        // "at capacity": lo que respondia Overpass al probarlo
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Un_Overpass_saturado_se_informa_como_HttpRequestException(HttpStatusCode estado)
    {
        var osm = new OsmFalso(_ => Json("# 502 Gateway Timeout", estado));

        await Assert.ThrowsAsync<HttpRequestException>(() => Mapa(osm).DescargarCasasAsync());
    }

    [Fact]
    public async Task La_zona_se_busca_solo_en_Argentina_y_queda_en_cache()
    {
        var osm = new OsmFalso(_ => Json(Grabada("nominatim-palermo.json")));
        var mapa = Mapa(osm);

        await mapa.UbicarAsync("  Palermo  ");
        await mapa.UbicarAsync("palermo");

        var url = Assert.Single(osm.Pedidos).Url.ToString();
        Assert.Contains("q=Palermo&", url);
        Assert.Contains("countrycodes=ar", url);
    }

    // ── La copia en la base y la busqueda de cercanas ──────────────────────────────────────────

    [Fact]
    public async Task La_busqueda_lee_la_copia_y_la_ubicacion_no_sale_hacia_el_mapa()
    {
        await ImportarGrabadasAsync();
        var osm = new OsmFalso(_ => throw new InvalidOperationException("La busqueda no tiene que salir al mapa."));

        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(osm)).BuscarCercaAsync(Obelisco);

        Assert.True(resultado.EsExitoso);
        Assert.NotEmpty(resultado.Valor.Casas);
        Assert.Equal(resultado.Valor.Casas.OrderBy(c => c.DistanciaMetros), resultado.Valor.Casas);
        Assert.All(resultado.Valor.Casas, c => Assert.InRange(c.DistanciaMetros, 0, 5000));
        Assert.Empty(osm.Pedidos);
    }

    [Fact]
    public async Task Respeta_el_maximo_de_casas()
    {
        await ImportarGrabadasAsync();

        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]"))), maximo: 3).BuscarCercaAsync(Obelisco);

        Assert.Equal(3, resultado.Valor.Casas.Count);
    }

    [Fact]
    public async Task Lejos_de_todas_las_casas_no_hay_resultados_pero_no_es_un_error()
    {
        await ImportarGrabadasAsync();

        await using var contexto = Contexto();
        var mendoza = new UbicacionDto { Latitud = -32.89, Longitud = -68.84 };
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]")))).BuscarCercaAsync(mendoza);

        Assert.True(resultado.EsExitoso);
        Assert.Empty(resultado.Valor.Casas);
    }

    [Fact]
    public async Task Sin_copia_importada_avisa_que_todavia_se_esta_cargando()
    {
        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]")))).BuscarCercaAsync(Obelisco);

        Assert.False(resultado.EsExitoso);
        Assert.Contains("Todavía estamos cargando", resultado.Error);
    }

    [Fact]
    public async Task Por_zona_ubica_con_Nominatim_y_busca_en_la_copia()
    {
        await ImportarGrabadasAsync();

        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json(Grabada("nominatim-palermo.json")))))
            .BuscarCercaDeAsync("Palermo");

        Assert.True(resultado.EsExitoso);
        Assert.StartsWith("Palermo", resultado.Valor.Centro.Descripcion);
    }

    [Fact]
    public async Task Una_zona_que_no_existe_lo_dice()
    {
        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]")))).BuscarCercaDeAsync("ningun lugar");

        Assert.False(resultado.EsExitoso);
        Assert.Contains("No encontramos \"ningun lugar\"", resultado.Error);
    }

    [Fact]
    public async Task Una_ubicacion_imposible_no_se_busca()
    {
        await using var contexto = Contexto();
        var resultado = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]"))))
            .BuscarCercaAsync(new UbicacionDto { Latitud = 120, Longitud = 0 });

        Assert.False(resultado.EsExitoso);
    }

    // ── La importacion del worker ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Si_Overpass_falla_queda_la_copia_anterior()
    {
        await ImportarGrabadasAsync();
        int antes;
        await using (var contexto = Contexto())
            antes = await contexto.CasasRepuestos.CountAsync();

        await using (var contexto = Contexto())
        {
            var saturado = Mapa(new OsmFalso(_ => Json("# 502", HttpStatusCode.BadGateway)));
            Assert.False((await Importacion(contexto, saturado).ImportarAsync()).EsExitoso);

            var vacio = Mapa(new OsmFalso(_ => Json("""{"elements": []}""")));
            Assert.False((await Importacion(contexto, vacio).ImportarAsync()).EsExitoso);
        }

        await using (var contexto = Contexto())
            Assert.Equal(antes, await contexto.CasasRepuestos.CountAsync());
    }

    /// <summary>
    /// Primer arranque con Overpass saturado: en vez de esperar, se carga la copia incluida (todas
    /// las casas de Argentina descargadas al armar la version). Se informa igual la falla, para que
    /// el worker reintente y la reemplace.
    /// </summary>
    [Fact]
    public async Task Con_la_base_vacia_y_Overpass_caido_se_carga_la_copia_incluida()
    {
        await using (var contexto = Contexto())
        {
            var saturado = Mapa(new OsmFalso(_ => Json("# 502", HttpStatusCode.BadGateway)));
            Assert.False((await Importacion(contexto, saturado).ImportarAsync()).EsExitoso);
        }

        await using (var contexto = Contexto())
        {
            Assert.InRange(await contexto.CasasRepuestos.CountAsync(), 2000, 3000);
            var cercanas = await Cercanas(contexto, Mapa(new OsmFalso(_ => Json("[]")))).BuscarCercaAsync(Obelisco);
            Assert.NotEmpty(cercanas.Valor.Casas);
        }
    }

    /// <summary>
    /// Overpass saturado puede devolver un JSON valido con una parte del pais. Con menos de la
    /// mitad de las casas guardadas, la descarga no reemplaza la copia y se informa como falla.
    /// </summary>
    [Fact]
    public async Task Una_descarga_con_menos_de_la_mitad_de_las_casas_no_reemplaza_la_copia()
    {
        await ImportarGrabadasAsync();
        int antes;
        await using (var contexto = Contexto())
            antes = await contexto.CasasRepuestos.CountAsync();
        Assert.True(antes >= 4, "La respuesta grabada tiene que traer varias casas para esta prueba.");

        const string recortada = """
            {"elements": [
              {"type": "node", "id": 900001, "lat": -34.6, "lon": -58.38, "tags": {"shop": "car_parts", "name": "Unica Casa"}}
            ]}
            """;
        await using (var contexto = Contexto())
        {
            var resultado = await Importacion(contexto, Mapa(new OsmFalso(_ => Json(recortada)))).ImportarAsync();
            Assert.False(resultado.EsExitoso);
        }

        await using (var contexto = Contexto())
        {
            Assert.Equal(antes, await contexto.CasasRepuestos.CountAsync());
            Assert.False(await contexto.CasasRepuestos.AnyAsync(c => c.Nombre == "Unica Casa"));
        }
    }

    [Fact]
    public async Task Volver_a_importar_reemplaza_la_copia_sin_duplicar()
    {
        await ImportarGrabadasAsync();
        await ImportarGrabadasAsync();

        await using var contexto = Contexto();
        var idsMapa = await contexto.CasasRepuestos.Select(c => c.IdMapa).ToListAsync();
        Assert.Equal(idsMapa.Count, idsMapa.Distinct().Count());
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    [Fact]
    public async Task Hace_falta_importar_si_no_hay_copia_o_tiene_mas_de_una_semana()
    {
        var mapa = Mapa(new OsmFalso(_ => Json(Grabada("overpass-centro.json"))));
        var hoy = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

        await using var contexto = Contexto();
        Assert.True(await Importacion(contexto, mapa, new RelojFijo(hoy)).HaceFaltaImportarAsync());

        await Importacion(contexto, mapa, new RelojFijo(hoy)).ImportarAsync();
        Assert.False(await Importacion(contexto, mapa, new RelojFijo(hoy.AddDays(6))).HaceFaltaImportarAsync());
        Assert.True(await Importacion(contexto, mapa, new RelojFijo(hoy.AddDays(7))).HaceFaltaImportarAsync());
    }
}
