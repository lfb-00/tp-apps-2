using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>Comportamiento general de la pantalla: tema, celular y navegacion.</summary>
public sealed class InterfazTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    [Fact]
    public async Task El_boton_de_tema_alterna_y_sin_sesion_se_recuerda_al_recargar()
    {
        await IrAsync("/");
        var html = Pagina.Locator("html");
        var inicial = await html.GetAttributeAsync("data-theme");
        var otro = inicial == "dark" ? "light" : "dark";

        await Pagina.Locator("button.btn-tema").ClickAsync();
        await Expect(html).ToHaveAttributeAsync("data-theme", otro);

        await Pagina.ReloadAsync();
        await EsperarListaAsync(Pagina);
        await Expect(html).ToHaveAttributeAsync("data-theme", otro);
    }

    [Fact]
    public async Task En_celular_el_menu_se_abre_y_navega()
    {
        var celular = await NuevaPaginaAsync(new ViewportSize { Width = 390, Height = 844 });
        await IrAsync(celular, "/");

        var menu = celular.Locator("nav.nav-mobile");
        await Expect(menu).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("abierto"));

        await celular.GetByRole(AriaRole.Button, new() { Name = "Abrir menú" }).ClickAsync();
        await Expect(menu).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("abierto"));

        await menu.GetByRole(AriaRole.Link, new() { Name = "Catálogo" }).ClickAsync();
        await Expect(celular.GetByRole(AriaRole.Heading, new() { Name = "Catálogo de repuestos" })).ToBeVisibleAsync();
        await Expect(menu).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("abierto"));
    }

    [Fact]
    public async Task En_celular_se_puede_buscar_sin_sesion()
    {
        var celular = await NuevaPaginaAsync(new ViewportSize { Width = 390, Height = 844 });
        await BuscarSinSesionAsync(celular, "vibra el volante cuando freno fuerte");

        await Expect(TarjetaIA(celular)).ToContainTextAsync("Probablemente: frenos");
        await Expect(Piezas(celular).First).ToBeVisibleAsync();

        // Nada se sale de la pantalla a lo ancho: en celular eso obliga a scrollear de costado.
        var desborde = await celular.EvaluateAsync<int>(
            "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
        Assert.True(desborde <= 0, $"La pagina se sale {desborde}px de la pantalla a lo ancho.");
    }

    [Fact]
    public async Task La_ruta_vieja_de_busquedas_redirige_a_la_portada()
    {
        await IrAsync("/busquedas");
        await Expect(Pagina).ToHaveURLAsync($"{Entorno.UrlWeb}/");
    }

    /// <summary>
    /// La parte del recorrido de una busqueda que se ve desde afuera de la Web: cada respuesta
    /// trae su X-Correlation-Id y, si el pedido ya traia uno, se respeta. Que despues viaje a
    /// Catalogo.Api, IA.Api y la cola lo cubren los logs (informe, seccion 5.3) y el issue #24.
    /// </summary>
    [Fact]
    public async Task La_Web_asigna_un_correlation_id_y_respeta_el_que_recibe()
    {
        var sinCabecera = await Pagina.APIRequest.GetAsync("/salud");
        Assert.False(string.IsNullOrWhiteSpace(sinCabecera.Headers.GetValueOrDefault("x-correlation-id")));

        var propio = $"e2e{Unico()}";
        var conCabecera = await Pagina.APIRequest.GetAsync("/salud", new()
        {
            Headers = new Dictionary<string, string> { ["X-Correlation-Id"] = propio }
        });
        Assert.Equal(propio, conCabecera.Headers.GetValueOrDefault("x-correlation-id"));
    }

    [Fact]
    public async Task Los_endpoints_de_salud_responden()
    {
        var respuesta = await Pagina.APIRequest.GetAsync("/salud");
        Assert.Equal(200, respuesta.Status);
        Assert.Contains("RepMatch.Web", await respuesta.TextAsync());
    }
}
