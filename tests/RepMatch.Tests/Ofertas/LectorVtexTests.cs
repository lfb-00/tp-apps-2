using System.Text.Json;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Fuentes.Vtex;

namespace RepMatch.Tests.Ofertas;

/// <summary>
/// El adapter VTEX contra respuestas reales grabadas de easy.com.ar y fravega.com (busqueda de
/// "bateria", noviembre de 2026, recortadas a los campos que importan). Fijan las reglas que salen
/// de esas respuestas: filtro de categoria, primera palabra del titulo y vendedor con stock.
/// </summary>
public class LectorVtexTests
{
    private static readonly ConsultaOfertasDto Bateria = new()
    {
        CodigoRepuesto = "BATE-12V-60AH",
        Nombre = "Bateria 12V 60Ah"
    };

    private static readonly OpcionesTiendaVtex Easy = new()
    {
        Nombre = "Easy",
        UrlBase = "https://www.easy.com.ar",
        PrefijosCategoria = ["/Automotor/"]
    };

    private static readonly OpcionesTiendaVtex Fravega = new()
    {
        Nombre = "Frávega",
        UrlBase = "https://www.fravega.com",
        PrefijosCategoria = ["/Autos, Motos y Otros/"]
    };

    internal static string Grabada(string nombre) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "vtex", nombre));

    [Fact]
    public void En_Easy_quedan_solo_las_baterias_de_auto()
    {
        var ofertas = LectorVtex.Leer(Grabada("easy-bateria.json"), Easy, Bateria, Bateria.Nombre, esCodigo: false);

        // De diez resultados: afuera las pilas, las baterias de herramientas, el taladro y los cargadores.
        Assert.Equal(3, ofertas.Count);
        Assert.All(ofertas, o => Assert.StartsWith("Batería", o.Titulo));
        Assert.All(ofertas, o => Assert.Equal(CoincidenciasOferta.ProbablePorNombre, o.Coincidencia));
        Assert.All(ofertas, o => Assert.Equal("ARS", o.Moneda));

        var enerbox = Assert.Single(ofertas, o => o.Titulo.Contains("Cb410"));
        Assert.Equal(100810m, enerbox.Precio);
        Assert.Equal(118600m, enerbox.PrecioLista);
        Assert.Equal("7797535000843", enerbox.Ean);
        Assert.Equal("Enerbox", enerbox.Marca);
        Assert.Equal("https://www.easy.com.ar/bateria-enerbox-tipo-12x65-cb410-1304479/p", enerbox.Url);
        Assert.Equal("BATE-12V-60AH", enerbox.CodigoRepuesto);
    }

    [Fact]
    public void Sin_filtro_de_categoria_se_colarian_baterias_de_herramientas()
    {
        var sinFiltro = new OpcionesTiendaVtex { Nombre = "Easy", UrlBase = "https://www.easy.com.ar" };

        var ofertas = LectorVtex.Leer(Grabada("easy-bateria.json"), sinFiltro, Bateria, Bateria.Nombre, esCodigo: false);

        Assert.Contains(ofertas, o => o.Titulo.Contains("Procore 18V"));
    }

    [Fact]
    public void Una_publicacion_con_un_enlace_que_no_es_http_se_descarta()
    {
        // El enlace termina en la pagina: "javascript:" tambien es una URL absoluta.
        var json = Grabada("easy-bateria.json").Replace(
            "https://www.easy.com.ar/bateria-enerbox-tipo-12x65-cb410-1304479/p", "javascript:alert(1)");

        var ofertas = LectorVtex.Leer(json, Easy, Bateria, Bateria.Nombre, esCodigo: false);

        Assert.Equal(2, ofertas.Count);
        Assert.DoesNotContain(ofertas, o => o.Titulo.Contains("Cb410"));
    }

    [Fact]
    public void En_Fravega_toma_el_vendedor_con_stock_y_no_el_de_la_tienda()
    {
        var ofertas = LectorVtex.Leer(Grabada("fravega-bateria.json"), Fravega, Bateria, Bateria.Nombre, esCodigo: false);

        // El inflador y el cable estan en Autos pero no son baterias.
        var elpra = Assert.Single(ofertas);
        Assert.Equal("Batería para auto Elpra 12050 12x50", elpra.Titulo);
        Assert.Equal("ELPRA", elpra.Vendedor);
        Assert.Equal(113000m, elpra.Precio);
        Assert.Null(elpra.PrecioLista);
        Assert.Null(elpra.Ean);
    }

    [Fact]
    public void Por_codigo_solo_acepta_publicaciones_que_lo_mencionan()
    {
        var porEan = LectorVtex.Leer(Grabada("easy-bateria.json"), Easy, Bateria, "7797535000843", esCodigo: true);
        var inexistente = LectorVtex.Leer(Grabada("easy-bateria.json"), Easy, Bateria, "W712/52", esCodigo: true);

        var oferta = Assert.Single(porEan);
        Assert.Equal(CoincidenciasOferta.ExactoPorEan, oferta.Coincidencia);
        Assert.Empty(inexistente);
    }

    [Fact]
    public void Distingue_el_codigo_de_publicacion_del_EAN()
    {
        var conReferenciaDeTienda = Bateria with { CodigosEquivalentes = ["1304479"] };

        var oferta = Assert.Single(LectorVtex.Leer(
            Grabada("easy-bateria.json"), Easy, conReferenciaDeTienda, "1304479", esCodigo: true));

        Assert.Equal(CoincidenciasOferta.ExactoPorCodigo, oferta.Coincidencia);
    }

    [Fact]
    public void Una_respuesta_que_no_es_una_lista_es_un_error_de_la_tienda()
    {
        Assert.ThrowsAny<JsonException>(() =>
            LectorVtex.Leer("""{"error":"bad request"}""", Easy, Bateria, Bateria.Nombre, esCodigo: false));
    }
}
