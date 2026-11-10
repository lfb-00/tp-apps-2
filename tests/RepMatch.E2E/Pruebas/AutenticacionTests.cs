using System.Text.RegularExpressions;
using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>Registro, ingreso y salida, con los errores que la persona puede cometer en cada uno.</summary>
public sealed class AutenticacionTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    [Fact]
    public async Task Ingresar_con_la_cuenta_de_la_semilla_muestra_el_nombre_en_el_encabezado()
    {
        await IngresarAsync(Pagina, Semilla.EmailCliente, Semilla.Contrasena);

        await Expect(Pagina).ToHaveURLAsync(new Regex("/$"));
        await Expect(Pagina.Locator(".user-nombre")).ToHaveTextAsync(Semilla.NombreCliente);
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "¿Qué le pasa a tu auto?" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Contrasena_incorrecta_y_email_inexistente_dan_el_mismo_mensaje()
    {
        // El mismo texto en los dos casos: distinguirlos le diria a un atacante que emails existen.
        await IrAsync("/login");
        await Pagina.Locator("#email").FillAsync(Semilla.EmailCliente);
        await Pagina.Locator("#contrasena").FillAsync("otra-clave-123");
        await Pagina.Locator("button.btn-full").ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Email o contraseña incorrectos.");

        await Pagina.Locator("#email").FillAsync($"nadie-{Unico()}@ejemplo.com");
        await Pagina.Locator("button.btn-full").ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Email o contraseña incorrectos.");

        await Expect(Pagina.Locator(".user-trigger")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Ingresar_con_los_campos_vacios_marca_los_dos_campos()
    {
        await IrAsync("/login");
        await Pagina.Locator("button.btn-full").ClickAsync();

        await Expect(Pagina.Locator(".error-campo", new() { HasText = "El email es obligatorio." })).ToBeVisibleAsync();
        await Expect(Pagina.Locator(".error-campo", new() { HasText = "La contraseña es obligatoria." })).ToBeVisibleAsync();
        await Expect(Pagina).ToHaveURLAsync(new Regex("/login$"));
    }

    [Fact]
    public async Task Registrarse_como_cliente_entra_a_la_portada_sin_vehiculos()
    {
        await RegistrarAsync(Pagina);

        await Expect(Pagina.GetByText("Todavía no cargaste ningún auto.")).ToBeVisibleAsync();
        await Expect(Pagina.GetByRole(AriaRole.Link, new() { Name = "Agregar un vehículo" })).ToBeVisibleAsync();
        await Expect(Pagina.GetByRole(AriaRole.Link, new() { Name = "Mi taller" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Registrarse_como_mecanico_lleva_al_taller_con_el_plan_gratuito()
    {
        await RegistrarAsync(Pagina, mecanico: true);

        await Expect(Pagina.Locator(".resumen-taller")).ToContainTextAsync("0 / 5");
        await Expect(Pagina.Locator(".resumen-taller")).ToContainTextAsync("Gratuito");
        await Expect(Pagina.GetByRole(AriaRole.Link, new() { Name = "Mi taller" }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Registrarse_con_un_email_que_ya_existe_muestra_el_error_y_no_entra()
    {
        await IrAsync("/login");
        await Pagina.GetByText("Creá una").ClickAsync();
        await Pagina.Locator("#nombre").FillAsync("Repetido");
        await Pagina.Locator("#email").FillAsync(Semilla.EmailCliente);
        await Pagina.Locator("#contrasena").FillAsync("Clave-e2e-123");
        await Pagina.Locator("#repetirContrasena").FillAsync("Clave-e2e-123");
        await Pagina.Locator("button.btn-full").ClickAsync();

        await Expect(Pagina.Locator(".alerta-error")).ToContainTextAsync("Ya existe un cliente con el email");
        await Expect(Pagina.Locator(".user-trigger")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Registro_valida_email_contrasena_corta_y_contrasenas_distintas()
    {
        await IrAsync("/login");
        await Pagina.GetByText("Creá una").ClickAsync();

        await Pagina.Locator("button.btn-full").ClickAsync();
        await Expect(Pagina.Locator(".error-campo", new() { HasText = "El nombre es obligatorio." })).ToBeVisibleAsync();

        await Pagina.Locator("#nombre").FillAsync("Ana");
        await Pagina.Locator("#email").FillAsync("sin-arroba");
        await Pagina.Locator("#contrasena").FillAsync("corta");
        await Pagina.Locator("#repetirContrasena").FillAsync("otra");
        await Pagina.Locator("button.btn-full").ClickAsync();

        await Expect(Pagina.Locator(".error-campo", new() { HasText = "El email no es válido." })).ToBeVisibleAsync();
        await Expect(Pagina.Locator(".error-campo", new() { HasText = "Mínimo 8 caracteres." })).ToBeVisibleAsync();
        await Expect(Pagina.Locator(".error-campo", new() { HasText = "Las contraseñas no coinciden." })).ToBeVisibleAsync();
        await Expect(Pagina).ToHaveURLAsync(new Regex("/login$"));
    }

    [Fact]
    public async Task Un_email_con_arroba_pero_mal_formado_lo_rechaza_el_servidor()
    {
        // La pantalla solo mira que tenga @; el validador del servidor es la frontera real.
        await IrAsync("/login");
        await Pagina.GetByText("Creá una").ClickAsync();
        await Pagina.Locator("#nombre").FillAsync("Ana");
        await Pagina.Locator("#email").FillAsync("ana@");
        await Pagina.Locator("#contrasena").FillAsync("Clave-e2e-123");
        await Pagina.Locator("#repetirContrasena").FillAsync("Clave-e2e-123");
        await Pagina.Locator("button.btn-full").ClickAsync();

        await Expect(Pagina.Locator(".alerta-error")).ToBeVisibleAsync();
        await Expect(Pagina.Locator(".user-trigger")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Cerrar_sesion_vuelve_al_estado_anonimo()
    {
        await IngresarAsync(Pagina, Semilla.EmailCliente, Semilla.Contrasena);
        await SalirAsync(Pagina);

        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Buscá la pieza para tu auto" })).ToBeVisibleAsync();
        await Expect(Pagina.GetByRole(AriaRole.Link, new() { Name = "Mi garage" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Recargar_la_pagina_mantiene_la_sesion()
    {
        await IngresarAsync(Pagina, Semilla.EmailCliente, Semilla.Contrasena);
        await IrAsync("/clientes");

        await Pagina.ReloadAsync();
        await EsperarListaAsync(Pagina);

        await Expect(Pagina.Locator(".user-nombre")).ToHaveTextAsync(Semilla.NombreCliente);
        await Expect(Pagina.GetByText("El Gol")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Con_sesion_iniciada_la_pantalla_de_login_redirige_a_la_portada()
    {
        await IngresarAsync(Pagina, Semilla.EmailCliente, Semilla.Contrasena);
        await IrAsync("/login");

        await Expect(Pagina).ToHaveURLAsync(new Regex("/$"));
    }
}
