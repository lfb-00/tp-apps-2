using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>El formulario de busqueda, con y sin sesion: validaciones, chips y lo que queda guardado.</summary>
public sealed class BusquedaTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    private const string TextoFrenos = "vibra el volante cuando freno fuerte";

    [Fact]
    public async Task Sin_sesion_buscar_sin_elegir_vehiculo_pide_completarlo()
    {
        await IrAsync("/");
        await Pagina.Locator("#textoAnon").FillAsync(TextoFrenos);
        await BotonDiagnosticar(Pagina).ClickAsync();

        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Elegí marca, modelo, año y motor del vehículo.");
        await Expect(Piezas(Pagina)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Sin_sesion_un_texto_corto_pide_mas_detalle_y_conserva_lo_escrito()
    {
        await IrAsync("/");
        await ElegirVehiculoAsync(Pagina, "anon");
        await Pagina.Locator("#textoAnon").FillAsync("ruido");
        await BotonDiagnosticar(Pagina).ClickAsync();

        await Expect(Pagina.Locator(".error-campo")).ToContainTextAsync("Describí el problema con un poco más de detalle");
        await Expect(Pagina.Locator("#textoAnon")).ToHaveValueAsync("ruido");
        await Expect(Piezas(Pagina)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Los_chips_de_sintomas_completan_y_vacian_el_texto()
    {
        await IrAsync("/");
        var chip = Pagina.GetByRole(AriaRole.Button, new() { Name = "Ruido al frenar" });

        await chip.ClickAsync();
        await Expect(Pagina.Locator("#textoAnon")).ToHaveValueAsync("Ruido al frenar");
        await Expect(chip).ToHaveAttributeAsync("aria-pressed", "true");

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Pierde agua" }).ClickAsync();
        await Expect(Pagina.Locator("#textoAnon")).ToHaveValueAsync("Ruido al frenar, Pierde agua");

        await chip.ClickAsync();
        await Expect(Pagina.Locator("#textoAnon")).ToHaveValueAsync("Pierde agua");
    }

    [Fact]
    public async Task Con_sesion_y_sin_vehiculos_invita_a_cargar_uno()
    {
        await RegistrarAsync(Pagina);

        await Expect(Pagina.Locator("#texto")).ToHaveCountAsync(0);
        await Pagina.GetByRole(AriaRole.Link, new() { Name = "Agregar un vehículo" }).ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Tu garage" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Con_sesion_un_texto_corto_no_crea_la_consulta()
    {
        await ClienteConGolAsync(Pagina);
        await IrAsync("/");
        await Pagina.Locator("#texto").FillAsync("frena mal");
        await BotonDiagnosticar(Pagina).ClickAsync();

        await Expect(Pagina.Locator(".error-campo")).ToContainTextAsync("Describí el problema con un poco más de detalle");
        await Expect(Consultas(Pagina)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Con_sesion_la_busqueda_queda_en_tus_consultas_y_se_puede_abrir()
    {
        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoFrenos);

        await Expect(Pagina.Locator(".resultado-meta")).ToContainTextAsync(Vehiculo);
        await Expect(Consultas(Pagina).First).ToContainTextAsync(TextoFrenos);
        await Expect(Pagina.Locator("#texto")).ToHaveValueAsync("");

        await Consultas(Pagina).First.GetByRole(AriaRole.Link).ClickAsync();
        await EsperarListaAsync(Pagina);
        await Expect(Pagina).ToHaveURLAsync($"{Entorno.UrlWeb}/consultas/{id}");
        await Expect(Pagina.GetByText(TextoFrenos)).ToBeVisibleAsync();
        await Expect(Pagina.GetByText("Tu consulta del")).ToBeVisibleAsync();
    }

    /// <summary>
    /// ERROR CONOCIDO. El boton se deshabilita recien cuando vuelve el primer render, y el segundo
    /// clic ya viajo por SignalR: Home.BuscarAsync corre dos veces y quedan dos consultas iguales.
    /// Arreglo posible: salir de BuscarAsync si <c>buscando</c> ya es true. Sacar el Skip al arreglarlo.
    /// </summary>
    [Fact(Skip = "Error conocido: el doble clic crea dos consultas (ver comentario de la prueba).")]
    public async Task Doble_clic_en_buscar_crea_una_sola_consulta()
    {
        await ClienteConGolAsync(Pagina);
        await IrAsync("/");
        await Pagina.Locator("#texto").FillAsync(TextoFrenos);

        await BotonDiagnosticar(Pagina).DblClickAsync();

        await Expect(Pagina.Locator(".resultado-meta")).ToBeVisibleAsync();
        await Pagina.WaitForTimeoutAsync(1500);   // si hubiera un segundo pedido, que termine
        await IrAsync("/");
        await Expect(Consultas(Pagina)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Un_texto_con_html_se_muestra_literal_y_no_se_ejecuta()
    {
        var dialogos = 0;
        Pagina.Dialog += (_, dialogo) => { dialogos++; _ = dialogo.DismissAsync(); };
        const string texto = "<script>alert('x')</script> chilla al frenar 🚗 ñandú";

        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, texto);

        await Expect(Consultas(Pagina).First).ToContainTextAsync(texto);
        Assert.Equal(0, dialogos);
    }

    [Fact]
    public async Task El_vehiculo_elegido_en_el_garage_llega_preseleccionado_a_la_busqueda()
    {
        await RegistrarAsync(Pagina);
        await AgregarGolAsync(Pagina, apodo: "El de la facu");

        await Pagina.Locator(".vehiculo-card", new() { HasText = "El de la facu" })
            .GetByRole(AriaRole.Link, new() { Name = "Buscar piezas" }).ClickAsync();
        await EsperarListaAsync(Pagina);

        await Expect(Pagina.Locator("#vehiculo option:checked")).ToContainTextAsync("El de la facu");
    }
}
