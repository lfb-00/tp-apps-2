using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// Que el componente de IA este bien enchufado a la app: la tarjeta de diagnostico, las piezas
/// que elige, lo que queda guardado y lo que pasa cuando no entiende el texto.
///
/// Se corren SIN clave de LLM: responde el clasificador local, que es determinista, y por eso se
/// pueden afirmar sistema y piezas. La exactitud del diagnostico no se mide aca: eso es una
/// prueba aparte sobre un conjunto de frases etiquetadas.
/// </summary>
public sealed class IATests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    /// <summary>Piezas de la semilla que le entran a un Gol 2015 1.6, de todos los sistemas.</summary>
    private const int PiezasCompatiblesGol = 7;

    [Fact]
    public async Task Sin_sesion_un_sintoma_de_frenos_muestra_el_diagnostico_y_solo_piezas_de_frenos()
    {
        await BuscarSinSesionAsync(Pagina, "vibra el volante cuando freno fuerte");

        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("Probablemente: frenos");
        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("Es una orientación automática");

        var cantidad = await Piezas(Pagina).CountAsync();
        Assert.InRange(cantidad, 1, PiezasCompatiblesGol - 1);
        foreach (var sistema in await Piezas(Pagina).Locator(".sistema").AllInnerTextsAsync())
            Assert.Equal("Frenos", sistema);
    }

    [Fact]
    public async Task Con_sesion_el_diagnostico_trae_urgencia_y_la_pieza_que_corresponde()
    {
        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, "pierde agua y se calienta cuando esta parado en el transito");

        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("Probablemente: refrigeración");
        await Expect(TarjetaIA(Pagina).Locator(".etiqueta")).ToHaveTextAsync("Urgencia alta");
        await Expect(Piezas(Pagina).GetByRole(AriaRole.Heading, new() { Name = "Bomba de agua" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task El_diagnostico_queda_guardado_en_la_consulta()
    {
        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, "pierde agua y se calienta cuando esta parado en el transito");

        await IrAsync($"/consultas/{id}");
        var tarjeta = Pagina.Locator(".tarjeta").First;
        await Expect(tarjeta).ToContainTextAsync("Refrigeración");
        await Expect(tarjeta).ToContainTextAsync("urgencia alta");
        await Expect(tarjeta.Locator(".lista-piezas")).ToContainTextAsync("BOMB-AGUA-VW-16");

        // Y sobrevive a una recarga: viene de la base, no del estado de la pantalla.
        await Pagina.ReloadAsync();
        await EsperarListaAsync(Pagina);
        await Expect(Pagina.Locator(".tarjeta").First).ToContainTextAsync("Refrigeración");
    }

    [Fact]
    public async Task Un_texto_que_no_describe_un_problema_avisa_y_muestra_todas_las_compatibles()
    {
        await BuscarSinSesionAsync(Pagina, "hola que tal, como andas hoy");

        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("No reconocimos el problema");
        await Expect(Piezas(Pagina)).ToHaveCountAsync(PiezasCompatiblesGol);
    }

    [Fact]
    public async Task Un_problema_sin_piezas_en_el_catalogo_lo_dice_y_muestra_todas_las_compatibles()
    {
        await BuscarSinSesionAsync(Pagina, "patina el embrague cuando subo una cuesta");

        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("Probablemente: transmisión");
        await Expect(TarjetaIA(Pagina)).ToContainTextAsync("Todavía no tenemos piezas de transmisión");
        await Expect(Piezas(Pagina)).ToHaveCountAsync(PiezasCompatiblesGol);
    }

    /// <summary>La Web principal busca los repuestos por SOAP; web-local, en proceso. Mismo resultado.</summary>
    [SkippableFact]
    public async Task La_Web_en_modo_Local_diagnostica_igual_que_la_principal_por_SOAP()
    {
        Skip.IfNot(await RespondeAsync(Entorno.UrlWebLocal),
            $"La Web en modo Local no responde en {Entorno.UrlWebLocal} (perfil 'local' de compose).");

        const string texto = "vibra el volante cuando freno fuerte";
        await BuscarSinSesionAsync(Pagina, texto);
        var remoto = await TarjetaIA(Pagina).Locator(".bloque-ia-titulo").InnerTextAsync();
        var piezasRemoto = await Piezas(Pagina).Locator("h2").AllInnerTextsAsync();

        var local = await NuevaPaginaAsync();
        await local.GotoAsync($"{Entorno.UrlWebLocal}/");
        await EsperarListaAsync(local);
        await ElegirVehiculoAsync(local, "anon");
        await local.Locator("#textoAnon").FillAsync(texto);
        await BotonDiagnosticar(local).ClickAsync();
        await Expect(local.Locator("p.conteo")).ToBeVisibleAsync();

        Assert.Equal(remoto, await TarjetaIA(local).Locator(".bloque-ia-titulo").InnerTextAsync());
        Assert.Equal(piezasRemoto, await Piezas(local).Locator("h2").AllInnerTextsAsync());
    }

    private static async Task<bool> RespondeAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            return (await http.GetAsync($"{url.TrimEnd('/')}/salud")).IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
