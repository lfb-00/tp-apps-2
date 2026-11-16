using RepMatch.Contracts;
using RepMatch.Fuentes.Vtex;

namespace RepMatch.Tests.Ofertas;

/// <summary>
/// Como se transforma un numero del catalogo para buscarlo en una tienda, y el nombre de la pieza
/// para el buscador de VTEX.
/// </summary>
public class CodigosRepuestoTests
{
    [Theory]
    [InlineData("MANN W 712/83", "W 712/83")]
    [InlineData("Bosch 0 986 221 060", "0 986 221 060")]
    [InlineData("Fras-le PD/580", "PD/580")]
    [InlineData("PU 9023 z", "PU 9023 z")]        // dos letras: es parte del numero, no una marca
    [InlineData("JZZ-698-151-AF", "JZZ-698-151-AF")]
    [InlineData("5U0-615-301-C", "5U0-615-301-C")]
    [InlineData("  MAHLE OX 834D ", "OX 834D")]
    public void Saca_la_marca_solo_cuando_la_hay(string codigo, string esperado)
    {
        Assert.Equal(esperado, CodigosRepuesto.SinMarca(codigo));
    }

    [Theory]
    [InlineData("MANN W 712/83", "W712/83")]
    [InlineData("MANN HU 7002 z", "HU7002z")]
    [InlineData("Bosch 0 986 221 060", "0986221060")]
    [InlineData("5U0-615-301-C", "5U0-615-301-C")]
    public void Para_buscar_va_sin_marca_ni_espacios(string codigo, string esperado)
    {
        Assert.Equal(esperado, CodigosRepuesto.ParaBuscar(codigo));
    }

    /// <summary>El buscador de VTEX ordena mal con palabras de relleno: "Juego de bujías Gol" trae
    /// discos de freno en Autodo, "bujías Gol" trae lo que corresponde.</summary>
    [Theory]
    [InlineData("Juego de bujías (1.6 8V)", "Gol", "bujías Gol")]
    [InlineData("Kit de embrague (placa y disco)", "Gol", "embrague Gol")]
    [InlineData("Filtro de aceite (cartucho)", "Hilux", "Filtro aceite Hilux")]
    [InlineData("Batería 12V 50Ah (tamaño H4)", null, "Batería 12V 50Ah")]
    public void La_sugerencia_busca_el_tipo_de_pieza_y_el_modelo(string nombre, string? modelo, string esperado)
    {
        Assert.Equal(esperado, FuenteVtex.TerminoParaSugerir(nombre, modelo));
    }

    [Theory]
    [InlineData("Filtro de aceite (cartucho)", "Filtro de aceite")]
    [InlineData("Discos de freno delanteros (par)", "Discos de freno delanteros")]
    [InlineData("Juego de bujías (1.6 8V)", "Juego de bujías")]
    [InlineData("Kit de embrague (placa y disco", "Kit de embrague")]
    [InlineData("Batería 12V 50Ah", "Batería 12V 50Ah")]
    public void El_nombre_se_busca_sin_lo_que_va_entre_parentesis(string nombre, string esperado)
    {
        Assert.Equal(esperado, FuenteVtex.TerminoDeNombre(nombre));
    }
}
