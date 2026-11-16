using System.Text.RegularExpressions;
using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// Precios de las tiendas (Servicio de Ofertas + adapter VTEX), con los tres roles. Corren contra
/// el simulador de tiendas (tests/RepMatch.TiendaSimulada), que responde con busquedas reales
/// grabadas de easy.com.ar y fravega.com: en "bateria", Easy tiene tres baterias de auto y Fravega
/// una. Las demas piezas no tienen ofertas, igual que en las tiendas reales.
/// </summary>
public sealed class OfertasTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    /// <summary>El clasificador local lo lleva a Electrico y a la bateria del Gol.</summary>
    private const string TextoBateria = "no arranca hace clic clic al girar la llave";
    private const string TextoFrenos = "chilla al frenar como un metal que raspa";
    private const string Bateria = "BATE-12V-50AH";

    private static ILocator Precios(IPage pagina, string codigo) =>
        pagina.Locator($".precios[data-codigo='{codigo}']");

    private static ILocator Ofertas(IPage pagina, string codigo) =>
        Precios(pagina, codigo).Locator("li.oferta");

    private static async Task MostrarProbablesAsync(IPage pagina, string codigo)
    {
        var detalles = Precios(pagina, codigo).Locator("details.ofertas-probables");
        if (await detalles.CountAsync() == 0)
            return;

        if (await detalles.GetAttributeAsync("open") is null)
            await detalles.Locator("summary").ClickAsync();

        await Expect(detalles).ToHaveAttributeAsync("open", "");
    }

    [Fact]
    public async Task El_cliente_ve_las_ofertas_de_la_mas_barata_a_la_mas_cara_despues_de_las_piezas()
    {
        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, TextoBateria);

        // Tres a la vista y el resto en la consulta: son cuatro entre las dos tiendas.
        await Expect(Ofertas(Pagina, Bateria)).ToHaveCountAsync(3);
        await Expect(Precios(Pagina, Bateria).Locator("p.mas")).ToHaveTextAsync("+1 oferta más");

        var precios = await Ofertas(Pagina, Bateria).Locator(".oferta-precio strong").AllTextContentsAsync();
        Assert.Equal(["$ 100.810", "$ 113.000", "$ 116.110"], precios);

        var primera = Ofertas(Pagina, Bateria).First;
        await Expect(primera).ToContainTextAsync("Batería 12X65 Cm Cb410 Enerbox");
        await Expect(primera.Locator(".oferta-precio s")).ToHaveTextAsync("$ 118.600");
        await Expect(primera).ToContainTextAsync("verificá que sea la medida de tu auto");
        await Expect(Ofertas(Pagina, Bateria).Nth(1)).ToContainTextAsync("Frávega · vende ELPRA");

        // Con las dos tiendas respondiendo no hay ningun aviso de error.
        await Expect(Pagina.Locator(".aviso-precios")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ir_a_la_tienda_registra_la_derivacion_y_redirige_a_la_publicacion()
    {
        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, TextoBateria);

        await Expect(Precios(Pagina, Bateria).Locator("details.ofertas-probables > summary"))
            .ToHaveTextAsync("Coincidencias probables por nombre (4)");
        await MostrarProbablesAsync(Pagina, Bateria);
        var enlace = Ofertas(Pagina, Bateria).First.GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" });
        var href = await enlace.GetAttributeAsync("href");
        Assert.Matches("^/derivar/[0-9a-f-]{36}$", href!);

        // Se sigue la redireccion a mano para no salir a la tienda real desde la prueba.
        var respuesta = await Pagina.APIRequest.GetAsync(href!, new() { MaxRedirects = 0 });
        Assert.Equal(302, respuesta.Status);
        Assert.Equal("https://www.easy.com.ar/bateria-enerbox-tipo-12x65-cb410-1304479/p", respuesta.Headers["location"]);
    }

    [Fact]
    public async Task En_la_consulta_estan_todas_las_ofertas_con_la_fecha_de_los_precios()
    {
        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoBateria);

        await IrAsync(Pagina, $"/consultas/{id}");

        var donde = Pagina.Locator(".donde-conseguirlas");
        await Expect(donde.Locator($".precios[data-codigo='{Bateria}'] li.oferta")).ToHaveCountAsync(4);
        await Expect(donde.Locator(".precios-pie")).ToContainTextAsync("Precios consultados el");
        await Expect(donde.Locator(".aviso-precios")).ToHaveCountAsync(0);

        await donde.GetByRole(AriaRole.Button, new() { Name = "Actualizar precios" }).ClickAsync();
        await Expect(donde.Locator($".precios[data-codigo='{Bateria}'] li.oferta")).ToHaveCountAsync(4);
    }

    /// <summary>
    /// Comprar una pieza no pide cuenta: sin sesion se ven las mismas ofertas. Como no hay oferta
    /// guardada, el enlace no es /derivar/{id} sino /ir/{token firmado}, que registra la
    /// derivacion sin cuenta (la comision) y redirige a la publicacion.
    /// </summary>
    [Fact]
    public async Task Sin_sesion_se_ven_las_ofertas_y_salir_a_la_tienda_redirige_a_la_publicacion()
    {
        await BuscarSinSesionAsync(Pagina, TextoBateria);

        await Expect(Precios(Pagina, Bateria).Locator("details.ofertas-probables > summary"))
            .ToHaveTextAsync("Coincidencias probables por nombre (4)");
        await MostrarProbablesAsync(Pagina, Bateria);
        var enlace = Ofertas(Pagina, Bateria).First.GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" });
        var href = await enlace.GetAttributeAsync("href");
        Assert.Matches("^/ir/[A-Za-z0-9_-]+$", href!);

        var respuesta = await Pagina.APIRequest.GetAsync(href!, new() { MaxRedirects = 0 });
        Assert.Equal(302, respuesta.Status);
        Assert.Equal("https://www.easy.com.ar/bateria-enerbox-tipo-12x65-cb410-1304479/p", respuesta.Headers["location"]);
    }

    /// <summary>
    /// Las pastillas no tienen ofertas en el simulador: aparece "¿Preferís comprarla en persona?".
    /// La zona escrita va a OpenStreetMap simulado (Nominatim ubica Palermo, Overpass devuelve los
    /// comercios grabados del centro de Buenos Aires).
    /// </summary>
    [Fact]
    public async Task Sin_ofertas_se_pueden_buscar_casas_de_repuestos_cerca_de_una_zona()
    {
        await BuscarSinSesionAsync(Pagina, TextoFrenos);
        var casas = Pagina.Locator(".casas-cercanas");
        await Expect(casas.GetByRole(AriaRole.Heading, new() { Name = "¿Preferís comprarla en persona?" })).ToBeVisibleAsync();

        await casas.GetByLabel("Barrio o ciudad").FillAsync("ningun lugar");
        await casas.GetByRole(AriaRole.Button, new() { Name = "Buscar", Exact = true }).ClickAsync();
        await Expect(casas.GetByRole(AriaRole.Alert)).ToContainTextAsync("No encontramos \"ningun lugar\"");

        await casas.GetByLabel("Barrio o ciudad").FillAsync("Palermo");
        await casas.GetByLabel("Barrio o ciudad").PressAsync("Enter");
        await Expect(casas.Locator(".casas-centro")).ToContainTextAsync("de Palermo");
        await Expect(casas.Locator("li.casa").First).ToBeVisibleAsync();
        await Expect(casas.Locator("li.casa").First.GetByRole(AriaRole.Link, new() { Name = "Ver en Google Maps" }))
            .ToHaveAttributeAsync("href", new Regex(@"^https://www\.google\.com/maps/search/\?api=1&query=-34\.\d+,-58\.\d+$"));
    }

    [Fact]
    public async Task Un_enlace_sin_cuenta_alterado_no_redirige_a_ningun_lado()
    {
        var respuesta = await Pagina.APIRequest.GetAsync("/ir/no-es-un-enlace-firmado", new() { MaxRedirects = 0 });

        Assert.Equal(404, respuesta.Status);
        Assert.Contains("venció o no es válido", await respuesta.TextAsync());
    }

    [Fact]
    public async Task El_mecanico_ve_los_precios_con_enlace_directo_y_sin_derivacion_a_su_nombre()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoBateria);
        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("Listo, se la compartiste.");

        await IrAsync(mecanicoPagina, $"/consultas/{id}");
        var ofertas = Ofertas(mecanicoPagina, Bateria);
        await Expect(ofertas).ToHaveCountAsync(4);
        await MostrarProbablesAsync(mecanicoPagina, Bateria);
        await Expect(ofertas.First.GetByRole(AriaRole.Link, new() { Name = "Ver en la tienda" }))
            .ToHaveAttributeAsync("href", "https://www.easy.com.ar/bateria-enerbox-tipo-12x65-cb410-1304479/p");
        await Expect(mecanicoPagina.Locator("a[href^='/derivar/']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Si_el_mecanico_recomienda_otra_pieza_el_cliente_ve_sus_precios_marcados()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoFrenos);
        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("Listo, se la compartiste.");

        // El diagnostico dijo frenos; el mecanico corrige y recomienda la bateria.
        await IrAsync(mecanicoPagina, $"/consultas/{id}");
        await mecanicoPagina.GetByLabel("No, recomiendo otras").CheckAsync();
        await mecanicoPagina.Locator("label.campo-check", new() { HasText = Bateria }).Locator("input").CheckAsync();
        await mecanicoPagina.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(Exito(mecanicoPagina)).ToContainTextAsync("Revisión enviada");

        await IrAsync(Pagina, $"/consultas/{id}");
        var ofertas = Ofertas(Pagina, Bateria);
        await Expect(ofertas).ToHaveCountAsync(4);
        await MostrarProbablesAsync(Pagina, Bateria);
        await Expect(ofertas.First).ToContainTextAsync("Recomendada por tu mecánico");
        await Expect(ofertas.First.GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" }))
            .ToHaveAttributeAsync("href", new Regex("^/derivar/"));
    }

    [Fact]
    public async Task Una_pieza_que_ninguna_tienda_publica_lo_dice()
    {
        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, TextoFrenos);

        await Expect(Pagina.Locator(".precios .precios-vacio").First)
            .ToHaveTextAsync("No tenemos ofertas de esta pieza por ahora.");
    }

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinTiendas)]
    public async Task Con_las_tiendas_caidas_se_ven_las_piezas_y_un_aviso()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinTiendas, "Corre solo con tienda-simulada frenada (scripts/e2e.sh).");

        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, TextoBateria);

        await Expect(Piezas(Pagina)).ToHaveCountAsync(1);
        await Expect(Pagina.Locator(".aviso-precios")).ToContainTextAsync("No pudimos consultar Easy ni Frávega");
        // No puede decir que nadie la publica: las tiendas no contestaron.
        await Expect(Precios(Pagina, Bateria).Locator(".precios-vacio"))
            .ToHaveTextAsync("Sin precios por ahora: no todas las tiendas respondieron.");
    }

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinTiendas)]
    public async Task Sin_sesion_y_con_las_tiendas_caidas_la_busqueda_sigue_andando()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinTiendas, "Corre solo con tienda-simulada frenada (scripts/e2e.sh).");

        await BuscarSinSesionAsync(Pagina, TextoBateria);

        await Expect(Piezas(Pagina)).ToHaveCountAsync(1);
        await Expect(Pagina.Locator(".aviso-precios")).ToContainTextAsync("No pudimos consultar");
        await Expect(Precios(Pagina, Bateria).Locator(".precios-vacio"))
            .ToHaveTextAsync("Sin precios por ahora: no todas las tiendas respondieron.");
    }
}
