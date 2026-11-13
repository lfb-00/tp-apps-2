using System.Text.RegularExpressions;
using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>Mi perfil: datos, foto, vehiculo predeterminado, tema, contrasenia y baja de la cuenta.</summary>
public sealed class PerfilTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    /// <summary>PNG de 1x1 valido: alcanza para que el navegador lo achique y lo mande.</summary>
    private static readonly byte[] PngMinimo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private ILocator Seccion(string titulo) =>
        Pagina.Locator("section.perfil-card").Filter(new() { Has = Pagina.GetByRole(AriaRole.Heading, new() { Name = titulo }) });

    [Fact]
    public async Task Guardar_telefono_provincia_y_localidad_persiste_al_recargar()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/perfil");

        await Pagina.Locator("#p-telefono").FillAsync("+54 9 11 5555-1234");
        await Pagina.GetByLabel("Este número tiene WhatsApp").CheckAsync();
        await Pagina.Locator("#p-provincia").SelectOptionAsync("Córdoba");
        await Pagina.Locator("#p-localidad").FillAsync("Nueva Córdoba");
        await Seccion("Datos personales").GetByRole(AriaRole.Button, new() { Name = "Guardar datos" }).ClickAsync();
        await Expect(Seccion("Datos personales").GetByRole(AriaRole.Status)).ToHaveTextAsync("Tus datos quedaron guardados.");

        await Pagina.ReloadAsync();
        await EsperarListaAsync(Pagina);
        await Expect(Pagina.Locator("#p-telefono")).ToHaveValueAsync("+54 9 11 5555-1234");
        await Expect(Pagina.GetByLabel("Este número tiene WhatsApp")).ToBeCheckedAsync();
        await Expect(Pagina.Locator("#p-provincia")).ToHaveValueAsync("Córdoba");
        await Expect(Pagina.Locator("#p-localidad")).ToHaveValueAsync("Nueva Córdoba");
    }

    [Fact]
    public async Task Un_telefono_con_letras_o_un_nombre_vacio_no_se_guardan()
    {
        var cuenta = await RegistrarAsync(Pagina);
        await IrAsync("/perfil");

        await Pagina.Locator("#p-telefono").FillAsync("llamame al once");
        await Pagina.Locator("#p-nombre").FillAsync("");
        await Seccion("Datos personales").GetByRole(AriaRole.Button, new() { Name = "Guardar datos" }).ClickAsync();

        await Expect(Pagina.Locator(".error-campo", new() { HasText = "Usá dígitos" })).ToBeVisibleAsync();
        await Expect(Pagina.Locator(".error-campo", new() { HasText = "El nombre es obligatorio." })).ToBeVisibleAsync();

        await Pagina.ReloadAsync();
        await EsperarListaAsync(Pagina);
        await Expect(Pagina.Locator("#p-nombre")).ToHaveValueAsync(cuenta.Nombre);
        await Expect(Pagina.Locator("#p-telefono")).ToHaveValueAsync("");
    }

    [Fact]
    public async Task Una_foto_valida_se_guarda_y_se_puede_quitar()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/perfil");
        var foto = Seccion("Foto de perfil");

        await foto.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "yo.png", MimeType = "image/png", Buffer = PngMinimo
        });
        await Expect(foto.GetByText("Vista previa. Guardala para usarla.")).ToBeVisibleAsync();
        await foto.GetByRole(AriaRole.Button, new() { Name = "Guardar foto" }).ClickAsync();
        await Expect(foto.GetByRole(AriaRole.Status)).ToHaveTextAsync("Tu foto quedó guardada.");
        await Expect(Pagina.Locator(".user-trigger img")).ToBeVisibleAsync();

        await foto.GetByRole(AriaRole.Button, new() { Name = "Quitar foto" }).ClickAsync();
        await Expect(foto.GetByRole(AriaRole.Status)).ToHaveTextAsync("Quitamos tu foto.");
    }

    [Fact]
    public async Task Un_archivo_que_no_es_imagen_o_una_imagen_de_mas_de_5_MB_se_rechazan()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/perfil");
        var archivo = Seccion("Foto de perfil").Locator("input[type=file]");

        await archivo.SetInputFilesAsync(new FilePayload
        {
            Name = "cv.pdf", MimeType = "application/pdf", Buffer = "%PDF-1.4 falso"u8.ToArray()
        });
        await Expect(Seccion("Foto de perfil").GetByRole(AriaRole.Alert))
            .ToHaveTextAsync("Formato no permitido. Usá JPG, PNG o WEBP.");

        await archivo.SetInputFilesAsync(new FilePayload
        {
            Name = "enorme.png", MimeType = "image/png", Buffer = new byte[5 * 1024 * 1024 + 1]
        });
        await Expect(Seccion("Foto de perfil").GetByRole(AriaRole.Alert))
            .ToHaveTextAsync("La imagen supera el máximo de 5 MB.");

        await Expect(Pagina.Locator(".user-trigger img")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task El_vehiculo_predeterminado_es_el_que_aparece_elegido_al_buscar()
    {
        await RegistrarAsync(Pagina);
        await AgregarGolAsync(Pagina, apodo: "Primero");
        await AgregarVehiculoAsync(Pagina, "Segundo", "Volkswagen", "Polo", "2020", "1.6 MSI");

        await IrAsync("/perfil");
        var opcion = Pagina.Locator("#p-vehiculo option", new() { HasText = "Segundo" });
        await Pagina.Locator("#p-vehiculo").SelectOptionAsync((await opcion.GetAttributeAsync("value"))!);
        await Seccion("Vehículo predeterminado").GetByRole(AriaRole.Button).ClickAsync();
        await Expect(Seccion("Vehículo predeterminado").GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Listo, ese vehículo aparece elegido al buscar.");

        await IrAsync("/");
        await Expect(Pagina.Locator("#vehiculo option:checked")).ToContainTextAsync("Segundo");
    }

    [Fact]
    public async Task El_tema_oscuro_se_guarda_en_la_cuenta_y_vuelve_en_otro_navegador()
    {
        var cuenta = await RegistrarAsync(Pagina);
        await IrAsync("/perfil");

        await Seccion("Apariencia").GetByLabel("Oscuro").CheckAsync();
        await Expect(Pagina.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await Expect(Seccion("Apariencia").GetByRole(AriaRole.Status)).ToHaveTextAsync("Tema oscuro guardado.");

        // Otro contexto no comparte localStorage: si vuelve oscuro, salio de la cuenta.
        var otra = await NuevaPaginaAsync();
        await IngresarAsync(otra, cuenta.Email, cuenta.Contrasena);
        await Expect(otra.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
    }

    [Fact]
    public async Task Cambiar_la_contrasena_valida_la_actual_y_despues_permite_entrar_con_la_nueva()
    {
        var cuenta = await RegistrarAsync(Pagina);
        await IrAsync("/perfil");
        var seccion = Seccion("Cambiar contraseña");
        var boton = seccion.GetByRole(AriaRole.Button, new() { Name = "Cambiar contraseña" });

        await Pagina.Locator("#p-actual").FillAsync(cuenta.Contrasena);
        await Pagina.Locator("#p-nueva").FillAsync("Nueva-clave-456");
        await Pagina.Locator("#p-repetir").FillAsync("Otra-cosa-789");
        await boton.ClickAsync();
        await Expect(seccion.Locator(".error-campo")).ToHaveTextAsync("Las contraseñas no coinciden.");

        await Pagina.Locator("#p-actual").FillAsync("no-es-la-actual");
        await Pagina.Locator("#p-repetir").FillAsync("Nueva-clave-456");
        await boton.ClickAsync();
        await Expect(seccion.GetByRole(AriaRole.Alert)).ToContainTextAsync("La contraseña actual es incorrecta.");

        await Pagina.Locator("#p-actual").FillAsync(cuenta.Contrasena);
        await boton.ClickAsync();
        await Expect(seccion.GetByRole(AriaRole.Status)).ToHaveTextAsync("Tu contraseña quedó cambiada.");

        await SalirAsync(Pagina);
        await IngresarAsync(Pagina, cuenta.Email, "Nueva-clave-456");
    }

    [Fact]
    public async Task Eliminar_la_cuenta_pide_la_contrasena_correcta_y_despues_no_deja_entrar()
    {
        var cuenta = await ClienteConGolAsync(Pagina);
        await IrAsync("/perfil");

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Eliminar cuenta" }).ClickAsync();
        var modal = Pagina.GetByRole(AriaRole.Dialog);
        var confirmar = modal.GetByRole(AriaRole.Button, new() { Name = "Eliminar definitivamente" });
        await Expect(confirmar).ToBeDisabledAsync();

        await modal.GetByLabel("Tu contraseña").FillAsync("no-es-esta-123");
        await confirmar.ClickAsync();
        await Expect(modal.GetByRole(AriaRole.Alert)).ToHaveTextAsync("La contraseña es incorrecta.");

        await modal.GetByLabel("Tu contraseña").FillAsync(cuenta.Contrasena);
        await confirmar.ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Status)).ToHaveTextAsync("Tu cuenta fue eliminada.");
        await Expect(Pagina.Locator(".btn-ingresar")).ToBeVisibleAsync();

        await IrAsync("/login");
        await Pagina.Locator("#email").FillAsync(cuenta.Email);
        await Pagina.Locator("#contrasena").FillAsync(cuenta.Contrasena);
        await Pagina.Locator("button.btn-full").ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Email o contraseña incorrectos.");
    }

    [Fact]
    public async Task Cancelar_o_Escape_cierran_el_modal_sin_borrar_nada()
    {
        await RegistrarAsync(Pagina);
        await IrAsync("/perfil");

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Eliminar cuenta" }).ClickAsync();
        await Pagina.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Cancelar" }).ClickAsync();
        await Expect(Pagina.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Eliminar cuenta" }).ClickAsync();
        await Pagina.GetByRole(AriaRole.Dialog).GetByLabel("Tu contraseña").PressAsync("Escape");
        await Expect(Pagina.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        await Expect(Pagina).ToHaveURLAsync(new Regex("/perfil$"));
        await Expect(Pagina.Locator(".user-trigger")).ToBeVisibleAsync();
    }
}
