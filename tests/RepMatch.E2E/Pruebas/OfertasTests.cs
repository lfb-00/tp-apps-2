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
    private const string Bateria = "BATE-12V-60AH";

    private static ILocator Precios(IPage pagina, string codigo) =>
        pagina.Locator($".precios[data-codigo='{codigo}']");

    private static ILocator Ofertas(IPage pagina, string codigo) =>
        Precios(pagina, codigo).Locator("li.oferta");

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

    [Fact]
    public async Task Sin_sesion_muestra_desde_cuanto_pero_no_donde()
    {
        await BuscarSinSesionAsync(Pagina, TextoBateria);

        await Expect(Precios(Pagina, Bateria).Locator(".precios-resumen")).ToHaveTextAsync(new Regex(@"Desde\s+\$ 100\.810\s+en 2 tiendas"));
        await Expect(Precios(Pagina, Bateria).GetByRole(AriaRole.Link, new() { Name = "Ingresá para ver dónde" }))
            .ToHaveAttributeAsync("href", "/login");
        await Expect(Pagina.GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" })).ToHaveCountAsync(0);
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
            .ToHaveTextAsync("Ninguna tienda conectada la publica por ahora.");
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
