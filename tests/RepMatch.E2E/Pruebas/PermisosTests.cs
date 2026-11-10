using System.Text.RegularExpressions;
using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// Quien puede ver que. Es lo que casi nunca se prueba a mano: entrar a una pagina sin sesion,
/// con el rol equivocado o a una consulta ajena pegando la URL.
/// </summary>
public sealed class PermisosTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    private const string NoEncontrada = "No encontramos esa consulta";

    [Fact]
    public async Task Sin_sesion_el_perfil_redirige_al_login()
    {
        await IrAsync("/perfil");
        await Expect(Pagina).ToHaveURLAsync(new Regex("/login$"));
    }

    [Fact]
    public async Task Sin_sesion_garage_taller_y_consultas_piden_ingresar()
    {
        await IrAsync("/clientes");
        await Expect(Pagina.GetByText("Ingresá para ver y administrar tus autos.")).ToBeVisibleAsync();

        await IrAsync("/taller");
        await Expect(Pagina.GetByText("Ingresá con tu cuenta de mecánico.")).ToBeVisibleAsync();

        await IrAsync($"/consultas/{Guid.NewGuid()}");
        await Expect(Pagina.GetByText("Ingresá para ver tus consultas.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Un_cliente_no_puede_usar_el_taller()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/taller");

        await Expect(Pagina.GetByText("Esta sección es para mecánicos. Tu cuenta es de cliente.")).ToBeVisibleAsync();
        await Expect(Pagina.GetByRole(AriaRole.Button, new() { Name = "Pasar a Pro" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Otro_cliente_que_pega_la_URL_de_una_consulta_ajena_no_la_ve()
    {
        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, "chilla al frenar como metal que raspa");

        var intruso = await NuevaPaginaAsync();
        await RegistrarAsync(intruso);
        await IrAsync(intruso, $"/consultas/{id}");

        await Expect(intruso.GetByRole(AriaRole.Heading, new() { Name = NoEncontrada })).ToBeVisibleAsync();
        await Expect(intruso.GetByText("chilla al frenar")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Un_mecanico_al_que_no_se_la_compartieron_no_la_ve()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, "chilla al frenar como metal que raspa");
        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");

        var otroMecanico = await NuevaPaginaAsync();
        await RegistrarAsync(otroMecanico, mecanico: true);
        await IrAsync(otroMecanico, $"/consultas/{id}");

        await Expect(otroMecanico.GetByRole(AriaRole.Heading, new() { Name = NoEncontrada })).ToBeVisibleAsync();
        await Expect(otroMecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task El_mecanico_asignado_ve_el_nombre_del_cliente_pero_no_su_email_ni_su_garage()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        var cliente = await RegistrarAsync(Pagina);
        await AgregarGolAsync(Pagina, apodo: "Apodo privado");
        var id = await BuscarConSesionAsync(Pagina, "chilla al frenar como metal que raspa");
        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");

        await IrAsync(mecanicoPagina, $"/consultas/{id}");
        await Expect(mecanicoPagina.GetByText($"Consulta de {cliente.Nombre}")).ToBeVisibleAsync();

        var contenido = await mecanicoPagina.Locator("main").InnerTextAsync();
        Assert.DoesNotContain(cliente.Email, contenido);
        Assert.DoesNotContain("Apodo privado", contenido);
    }

    [Fact]
    public async Task Una_consulta_que_no_existe_y_una_URL_mal_formada_no_rompen_la_app()
    {
        await RegistrarAsync(Pagina);

        await IrAsync($"/consultas/{Guid.NewGuid()}");
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = NoEncontrada })).ToBeVisibleAsync();

        var respuesta = await Pagina.GotoAsync("/consultas/no-es-un-id");
        Assert.Equal(404, respuesta!.Status);
    }

    [Fact]
    public async Task Despues_de_cerrar_sesion_el_boton_atras_no_muestra_datos_privados()
    {
        var cuenta = await RegistrarAsync(Pagina);
        await IrAsync("/perfil");
        await Expect(Pagina.Locator("#p-email")).ToHaveValueAsync(cuenta.Email);

        await SalirAsync(Pagina);
        await Pagina.GoBackAsync();
        await EsperarListaAsync(Pagina);

        await Expect(Pagina.Locator("#p-email")).ToHaveCountAsync(0);
        Assert.DoesNotContain(cuenta.Email, await Pagina.Locator("main").InnerTextAsync());
    }

    [Fact]
    public async Task Derivar_a_una_oferta_inexistente_responde_404_con_un_mensaje()
    {
        var respuesta = await Pagina.APIRequest.GetAsync($"/derivar/{Guid.NewGuid()}",
            new() { MaxRedirects = 0 });

        Assert.Equal(404, respuesta.Status);
        Assert.Contains("La oferta ya no existe.", await respuesta.TextAsync());
    }
}
