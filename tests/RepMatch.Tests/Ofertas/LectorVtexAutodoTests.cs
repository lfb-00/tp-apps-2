using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Fuentes.Vtex;

namespace RepMatch.Tests.Ofertas;

/// <summary>
/// El adapter VTEX contra una respuesta real grabada de autodo.com.ar (busqueda de "W712/83",
/// noviembre de 2026, recortada a los campos que importan). A diferencia de Easy y Fravega, Autodo
/// es una casa de repuestos: cada publicacion trae el numero del fabricante en la referencia y en
/// sus especificaciones los codigos originales y las equivalencias de otras marcas. La respuesta
/// tambien trae dos kits de filtros que incluyen el W 712/83 y no son la pieza.
/// </summary>
public class LectorVtexAutodoTests
{
    private const string Grabada = "autodo-filtro-w712-83.json";

    private static readonly OpcionesTiendaVtex Autodo = new()
    {
        Nombre = "Autodo",
        UrlBase = "https://www.autodo.com.ar",
        PrefijosCategoria = ["/Filtros y Aceites/", "/Refrigeración/"]
    };

    private static ConsultaOfertasDto FiltroHilux(params string[] codigos) => new()
    {
        CodigoRepuesto = "FILT-ACE-TOY-HILUX-28",
        Nombre = "Filtro de aceite",
        CodigosEquivalentes = codigos
    };

    private static IReadOnlyList<OfertaExternaDto> Leer(ConsultaOfertasDto consulta, string termino, bool esCodigo) =>
        LectorVtex.Leer(LectorVtexTests.Grabada(Grabada), Autodo, consulta, termino, esCodigo);

    [Fact]
    public void El_numero_del_catalogo_con_la_marca_adelante_encuentra_la_referencia_de_la_tienda()
    {
        // El catalogo dice "MANN W 712/83"; Autodo publica la referencia "W712-83-003".
        var oferta = Assert.Single(Leer(FiltroHilux("MANN W 712/83"), "W712/83", esCodigo: true));

        Assert.Equal("Filtro aceite Toyota Camry, Hilux Mann Filter", oferta.Titulo);
        Assert.Equal(11041.88m, oferta.Precio);
        Assert.Equal(CoincidenciasOferta.ExactoPorCodigo, oferta.Coincidencia);
        Assert.Equal("https://www.autodo.com.ar/mann-filter-w712-83/p", oferta.Url);
    }

    [Fact]
    public void Los_kits_que_incluyen_la_pieza_no_son_la_pieza()
    {
        var ofertas = Leer(FiltroHilux("MANN W 712/83"), "W712/83", esCodigo: true);

        Assert.DoesNotContain(ofertas, o => o.Titulo.StartsWith("Kit", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Bosch 0 986 452 002")]   // "Equivalencia: BOSCH: 0986452002"
    [InlineData("90915-YZZJ4")]           // "Código original", numero de Toyota
    public void Un_codigo_que_la_tienda_publica_como_original_o_equivalente_es_exacto(string codigo)
    {
        var oferta = Assert.Single(Leer(FiltroHilux(codigo), "W712/83", esCodigo: true));

        Assert.Equal(CoincidenciasOferta.ExactoPorCodigo, oferta.Coincidencia);
        Assert.Equal(11041.88m, oferta.Precio);
    }

    [Fact]
    public void Las_equivalencias_se_comparan_numero_por_numero_y_no_pegadas()
    {
        // "BOSCH: 0986452002<br>• VALEO: 586073" pegado daria este numero, que no esta publicado.
        Assert.Empty(Leer(FiltroHilux("Bosch 0986452002586073"), "W712/83", esCodigo: true));
    }

    [Fact]
    public void Lo_que_llega_por_nombre_es_exacto_si_menciona_un_codigo_de_la_pieza()
    {
        var ofertas = Leer(FiltroHilux("MANN W 712/83"), "Filtro de aceite", esCodigo: false);

        Assert.Equal(CoincidenciasOferta.ExactoPorCodigo,
            Assert.Single(ofertas, o => o.Titulo.Contains("Hilux", StringComparison.Ordinal)).Coincidencia);
        // Los otros filtros de aceite quedan como probables por nombre; kits y termostatos, afuera.
        Assert.All(ofertas.Where(o => !o.Titulo.Contains("Hilux", StringComparison.Ordinal)),
            o => Assert.Equal(CoincidenciasOferta.ProbablePorNombre, o.Coincidencia));
        Assert.All(ofertas, o => Assert.StartsWith("Filtro", o.Titulo));
    }
}
