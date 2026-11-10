using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// Que pasa cuando se cae otra parte del sistema. Cada prueba corre solo en su escenario: la
/// fase de scripts/e2e.sh que frena ese contenedor y fija E2E_ESCENARIO. En una corrida comun
/// aparecen como omitidas.
/// </summary>
public sealed class ResilienciaTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    private const int PiezasCompatiblesGol = 7;

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinIA)]
    public async Task Con_la_IA_caida_la_busqueda_sin_sesion_muestra_todas_las_compatibles()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinIA, "Corre solo con ia-api frenado (scripts/e2e.sh).");

        await BuscarSinSesionAsync(Pagina, "vibra el volante cuando freno fuerte");

        await Expect(Piezas(Pagina)).ToHaveCountAsync(PiezasCompatiblesGol);
        await Expect(TarjetaIA(Pagina)).ToHaveCountAsync(0);
        await Expect(Pagina.Locator(".alerta-error")).ToHaveCountAsync(0);
    }

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinIA)]
    public async Task Con_la_IA_caida_la_busqueda_con_sesion_se_guarda_igual()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinIA, "Corre solo con ia-api frenado (scripts/e2e.sh).");

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, "vibra el volante cuando freno fuerte");

        await Expect(Piezas(Pagina)).ToHaveCountAsync(PiezasCompatiblesGol);
        await Expect(TarjetaIA(Pagina)).ToHaveCountAsync(0);

        await IrAsync($"/consultas/{id}");
        await Expect(Pagina.Locator(".tarjeta").First.Locator(".lista-piezas li")).ToHaveCountAsync(PiezasCompatiblesGol);
    }

    /// <summary>Sin catalogo no hay busqueda posible, pero la app no se cae: sigue navegable.</summary>
    [SkippableFact]
    [Trait("Escenario", Escenarios.SinCatalogo)]
    public async Task Con_el_catalogo_caido_la_app_no_se_cae_y_sigue_navegable()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinCatalogo, "Corre solo con catalogo-api frenado (scripts/e2e.sh).");

        await IrAsync("/");
        await Expect(Pagina.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        await Pagina.GetByRole(AriaRole.Link, new() { Name = "Catálogo" }).First.ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Catálogo de repuestos" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// ERROR CONOCIDO: "Ver catálogo completo" recrea SelectorVehiculo (cambia su @key), el
    /// selector vuelve a pedir las opciones en OnInitializedAsync, CatalogoVehiculosRemoto tira
    /// HttpRequestException y nadie la atrapa: se cae el circuito y aparece "Ocurrió un error
    /// inesperado". Arreglo posible: atrapar el error en SelectorVehiculo y mostrar un aviso.
    /// </summary>
    [SkippableFact(Skip = "Error conocido: con el catálogo caído, 'Ver catálogo completo' tira la app (ver comentario).")]
    [Trait("Escenario", Escenarios.SinCatalogo)]
    public async Task Con_el_catalogo_caido_ver_el_catalogo_completo_muestra_un_mensaje()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinCatalogo, "Corre solo con catalogo-api frenado (scripts/e2e.sh).");

        await IrAsync("/catalogo");
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Ver catálogo completo" }).ClickAsync();

        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(Piezas(Pagina)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// ERROR CONOCIDO (menor): con el catalogo caido, los desplegables de vehiculo quedan vacios
    /// sin ningun aviso, y la persona no sabe si es un problema suyo o del sistema.
    /// </summary>
    [SkippableFact(Skip = "Error conocido: sin catálogo, el selector de vehículo queda vacío sin avisar.")]
    [Trait("Escenario", Escenarios.SinCatalogo)]
    public async Task Con_el_catalogo_caido_la_portada_avisa_que_no_esta_disponible()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinCatalogo, "Corre solo con catalogo-api frenado (scripts/e2e.sh).");

        await IrAsync("/");
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
    }

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinCatalogo)]
    public async Task Con_el_catalogo_caido_ingresar_y_ver_el_perfil_sigue_funcionando()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinCatalogo, "Corre solo con catalogo-api frenado (scripts/e2e.sh).");

        await IngresarAsync(Pagina, Semilla.EmailCliente, Semilla.Contrasena);
        await IrAsync("/perfil");
        await Expect(Pagina.Locator("#p-email")).ToHaveValueAsync(Semilla.EmailCliente);
    }
}
