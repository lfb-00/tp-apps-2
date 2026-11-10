using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>Catalogo de repuestos (publico) y garage de vehiculos (con sesion).</summary>
public sealed class CatalogoYGarageTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    [Fact]
    public async Task El_catalogo_completo_lista_piezas_sin_sesion()
    {
        await IrAsync("/catalogo");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Ver catálogo completo" }).ClickAsync();

        await Expect(Pagina.Locator("p.conteo")).ToContainTextAsync("en el catálogo");
        Assert.True(await Piezas(Pagina).CountAsync() > 10);
    }

    [Fact]
    public async Task Filtrar_por_vehiculo_y_sistema_deja_solo_ese_sistema()
    {
        await IrAsync("/catalogo");
        await ElegirVehiculoAsync(Pagina, "catalogo");
        await Pagina.Locator("#sistema").SelectOptionAsync("Frenos");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Buscar compatibles" }).ClickAsync();

        await Expect(Piezas(Pagina).First).ToBeVisibleAsync();
        foreach (var sistema in await Piezas(Pagina).Locator(".sistema").AllInnerTextsAsync())
            Assert.Equal("Frenos", sistema);
    }

    [Fact]
    public async Task Buscar_compatibles_sin_vehiculo_pide_completarlo()
    {
        await IrAsync("/catalogo");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Buscar compatibles" }).ClickAsync();

        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Elegí marca, modelo, año y motor del vehículo.");
    }

    [Fact]
    public async Task Un_vehiculo_sin_piezas_cargadas_lo_avisa()
    {
        await IrAsync("/catalogo");
        await ElegirVehiculoAsync(Pagina, "catalogo", "Volkswagen", "Amarok", "2024", "3.0 V6 TDI");
        await Pagina.Locator("#sistema").SelectOptionAsync("Direccion");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Buscar compatibles" }).ClickAsync();

        await Expect(Pagina.GetByText("No encontramos piezas para")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Agregar_un_vehiculo_lo_suma_al_garage_y_al_buscador()
    {
        await RegistrarAsync(Pagina);
        await AgregarGolAsync(Pagina, apodo: "El rojo");

        await Expect(Pagina.Locator(".vehiculo-card", new() { HasText = "El rojo" })).ToContainTextAsync(Vehiculo);

        await IrAsync("/");
        await Expect(Pagina.Locator("#vehiculo")).ToContainTextAsync("El rojo");
    }

    [Fact]
    public async Task Agregar_un_vehiculo_sin_completar_el_selector_muestra_el_error()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/clientes");
        await Pagina.Locator("#garage-marca").SelectOptionAsync("Volkswagen");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Agregar vehículo" }).ClickAsync();

        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Elegí marca, modelo, año y motor del vehículo.");
        await Expect(Pagina.GetByText("Todavía no cargaste ningún vehículo.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Agregar_dos_veces_el_mismo_vehiculo_lo_rechaza()
    {
        await RegistrarAsync(Pagina);
        await AgregarGolAsync(Pagina);

        await ElegirVehiculoAsync(Pagina, "garage");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Agregar vehículo" }).ClickAsync();

        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToContainTextAsync("ya tiene registrado un Volkswagen Gol 2015 (1.6)");
        await Expect(Pagina.Locator(".vehiculo-card")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Cambiar_la_marca_reinicia_modelo_anio_y_motor()
    {
        await IrAsync("/catalogo");
        await ElegirVehiculoAsync(Pagina, "catalogo");

        await Pagina.Locator("#catalogo-marca").SelectOptionAsync("Toyota");

        await Expect(Pagina.Locator("#catalogo-modelo")).ToHaveValueAsync("");
        await Expect(Pagina.Locator("#catalogo-anio")).ToBeDisabledAsync();
        await Expect(Pagina.Locator("#catalogo-motor")).ToBeDisabledAsync();
    }
}
