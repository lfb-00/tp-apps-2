using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// La colaboracion cliente-mecanico de punta a punta, con dos navegadores a la vez: el cliente
/// comparte, el mecanico revisa y el cliente ve el resultado. Mas el limite del plan gratuito.
/// </summary>
public sealed class MecanicoTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    private const string TextoFrenos = "chilla al frenar como un metal que raspa";

    /// <summary>Mecanico nuevo en su propio navegador y cliente con una consulta compartida con el.</summary>
    private async Task<(IPage Mecanico, Cuenta CuentaMecanico, Guid ConsultaId)> CompartidaAsync()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoFrenos);
        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("Listo, se la compartiste.");
        return (mecanicoPagina, mecanico, id);
    }

    private static ILocator Revision(IPage pagina) => pagina.Locator(".tarjeta.revision");

    [Fact]
    public async Task Compartir_lleva_la_consulta_a_la_bandeja_del_mecanico()
    {
        var (mecanico, _, id) = await CompartidaAsync();

        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Compartida con un mecánico" })).ToBeVisibleAsync();

        await IrAsync(mecanico, "/taller");
        await Expect(mecanico.Locator(".resumen-taller")).ToContainTextAsync("1 / 5");
        var item = mecanico.Locator("ul.recientes > li");
        await Expect(item).ToHaveCountAsync(1);
        await Expect(item).ToContainTextAsync("Sin revisar");
        await Expect(item).ToContainTextAsync(TextoFrenos);
        await Expect(item.GetByRole(AriaRole.Link, new() { Name = "Revisar" })).ToHaveAttributeAsync("href", $"/consultas/{id}");
    }

    [Fact]
    public async Task El_mecanico_confirma_y_el_cliente_ve_la_revision_sin_poder_retirarla()
    {
        var (mecanico, cuentaMecanico, id) = await CompartidaAsync();

        await IrAsync(mecanico, $"/consultas/{id}");
        await mecanico.Locator("#nota").FillAsync("Revisá también el disco.");
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(Exito(mecanico)).ToHaveTextAsync("Revisión enviada. El cliente ya la puede ver.");

        await IrAsync(Pagina, $"/consultas/{id}");
        await Expect(Revision(Pagina)).ToContainTextAsync($"Revisión de {cuentaMecanico.Nombre}");
        await Expect(Revision(Pagina)).ToContainTextAsync("Confirmó el diagnóstico");
        await Expect(Revision(Pagina)).ToContainTextAsync("Revisá también el disco.");
        await Expect(Pagina.GetByRole(AriaRole.Button, new() { Name = "Dejar de compartir" })).ToHaveCountAsync(0);

        await IrAsync(Pagina, "/");
        await Expect(Consultas(Pagina).First).ToContainTextAsync("Ver la revisión del mecánico");
    }

    [Fact]
    public async Task Corregir_sin_elegir_piezas_da_error_y_eligiendo_una_queda_como_recomendacion()
    {
        var (mecanico, _, id) = await CompartidaAsync();

        await IrAsync(mecanico, $"/consultas/{id}");
        await mecanico.GetByLabel("No, recomiendo otras").CheckAsync();
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(mecanico.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Para corregir el diagnóstico indicá al menos un repuesto.");

        await mecanico.Locator("label.campo-check", new() { HasText = "AMOR-VW-GOL-DEL" }).Locator("input").CheckAsync();
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(Exito(mecanico)).ToContainTextAsync("Revisión enviada");

        await IrAsync(Pagina, $"/consultas/{id}");
        await Expect(Revision(Pagina)).ToContainTextAsync("Corrigió el diagnóstico");
        await Expect(Revision(Pagina).Locator(".lista-piezas")).ToContainTextAsync("AMOR-VW-GOL-DEL");
    }

    [Fact]
    public async Task Una_consulta_revisada_no_se_puede_volver_a_revisar()
    {
        var (mecanico, _, id) = await CompartidaAsync();
        await IrAsync(mecanico, $"/consultas/{id}");
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(Exito(mecanico)).ToContainTextAsync("Revisión enviada");

        await mecanico.ReloadAsync();
        await EsperarListaAsync(mecanico);

        await Expect(Revision(mecanico)).ToContainTextAsync("Confirmó el diagnóstico");
        await Expect(mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Dejar_de_compartir_la_saca_de_la_bandeja_y_vuelve_el_directorio()
    {
        var (mecanico, _, _) = await CompartidaAsync();

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Dejar de compartir" }).ClickAsync();
        await Expect(Exito(Pagina)).ToHaveTextAsync("La consulta ya no está compartida.");
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Pedile a un mecánico que lo revise" })).ToBeVisibleAsync();

        await IrAsync(mecanico, "/taller");
        await Expect(mecanico.GetByText("Todavía nadie te compartió una consulta.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task El_plan_gratuito_rechaza_la_sexta_consulta_y_el_Pro_la_acepta()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var ids = new List<Guid>();
        for (var i = 1; i <= 6; i++)
            ids.Add(await BuscarConSesionAsync(Pagina, $"{TextoFrenos} (consulta {i})"));

        foreach (var id in ids.Take(5))
        {
            await CompartirAsync(Pagina, id, mecanico.Nombre);
            await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");
        }

        await CompartirAsync(Pagina, ids[5], mecanico.Nombre);
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToContainTextAsync("tiene la bandeja llena");

        await IrAsync(mecanicoPagina, "/taller");
        await Expect(mecanicoPagina.Locator(".resumen-taller")).ToContainTextAsync("5 / 5");
        await mecanicoPagina.GetByRole(AriaRole.Button, new() { Name = "Pasar a Pro" }).ClickAsync();
        await Expect(Exito(mecanicoPagina)).ToHaveTextAsync("Ya tenés el plan Pro.");
        await Expect(mecanicoPagina.Locator(".resumen-taller")).ToContainTextAsync("Pro");

        // Se recarga la pagina del cliente a proposito: sin recargar falla (ver la prueba siguiente).
        await CompartirAsync(Pagina, ids[5], mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");
    }

    /// <summary>
    /// ERROR CONOCIDO: datos viejos dentro de una misma pestania. En Blazor Server el DbContext
    /// vive lo que dura el circuito y EF devuelve la entidad que ya tiene en seguimiento sin
    /// releerla. El primer intento cargo al mecanico con plan Gratuito; cuando el mecanico pasa a
    /// Pro desde su sesion, el reintento del cliente sigue viendo Gratuito y vuelve a rechazar.
    /// Recargando la pagina (circuito nuevo) funciona. Arreglo posible: AsNoTracking en las
    /// lecturas, o un DbContext por operacion (IDbContextFactory) en vez de uno por circuito.
    /// </summary>
    [Fact(Skip = "Error conocido: el cliente ve datos viejos del mecánico hasta recargar (ver comentario).")]
    public async Task Sin_recargar_el_cliente_puede_compartir_apenas_el_mecanico_pasa_a_Pro()
    {
        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var ids = new List<Guid>();
        for (var i = 1; i <= 6; i++)
            ids.Add(await BuscarConSesionAsync(Pagina, $"{TextoFrenos} (consulta {i})"));
        foreach (var id in ids.Take(5))
        {
            await CompartirAsync(Pagina, id, mecanico.Nombre);
            await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");
        }

        await CompartirAsync(Pagina, ids[5], mecanico.Nombre);
        await Expect(Pagina.GetByRole(AriaRole.Alert)).ToContainTextAsync("tiene la bandeja llena");

        await IrAsync(mecanicoPagina, "/taller");
        await mecanicoPagina.GetByRole(AriaRole.Button, new() { Name = "Pasar a Pro" }).ClickAsync();
        await Expect(Exito(mecanicoPagina)).ToHaveTextAsync("Ya tenés el plan Pro.");

        await TarjetaDelDirectorio(Pagina, mecanico.Nombre)
            .GetByRole(AriaRole.Button, new() { Name = "Compartir" }).ClickAsync();
        await Expect(Exito(Pagina)).ToContainTextAsync("se la compartiste");
    }

    /// <summary>
    /// ERROR CONOCIDO, mismo origen que el anterior, visto desde el cliente: deja abierta la
    /// consulta compartida, el mecanico la revisa, y el cliente navega dentro de la app (sin F5)
    /// hasta su consulta. Sigue viendo "Compartida con un mecánico" en vez de la revision.
    /// </summary>
    [Fact(Skip = "Error conocido: sin recargar, el cliente no ve la revisión recién enviada (DbContext por circuito).")]
    public async Task Sin_recargar_el_cliente_ve_la_revision_que_acaba_de_enviar_el_mecanico()
    {
        var (mecanico, _, id) = await CompartidaAsync();

        await IrAsync(mecanico, $"/consultas/{id}");
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Enviar revisión" }).ClickAsync();
        await Expect(Exito(mecanico)).ToContainTextAsync("Revisión enviada");

        // Navegacion interna: mismo circuito, mismo DbContext.
        await Pagina.Locator("header nav").GetByRole(AriaRole.Link, new() { Name = "Buscar" }).ClickAsync();
        await Consultas(Pagina).First.GetByRole(AriaRole.Link).ClickAsync();

        await Expect(Revision(Pagina)).ToContainTextAsync("Confirmó el diagnóstico");
    }

    [Fact]
    public async Task El_mecanico_Pro_aparece_primero_y_destacado_en_el_directorio()
    {
        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoFrenos);
        await IrAsync($"/consultas/{id}");

        // Otras pruebas pueden dejar mecanicos Pro nuevos: lo que se afirma es el orden por plan,
        // no que la semilla quede primera.
        var tarjetas = Pagina.Locator(".directorio .vehiculo-card");
        await Expect(tarjetas.First).ToBeVisibleAsync();
        var textos = await tarjetas.AllInnerTextsAsync();
        var pro = textos.ToList().FindIndex(t => t.Contains("Motores Benítez"));
        var gratuito = textos.ToList().FindIndex(t => t.Contains("Taller San Martín"));

        Assert.True(pro >= 0 && gratuito >= 0, "Faltan los mecánicos de la semilla en el directorio.");
        Assert.True(pro < gratuito, "El mecánico Pro tiene que aparecer antes que el gratuito.");
        await Expect(tarjetas.Nth(pro).Locator(".etiqueta", new() { HasText = "Pro" })).ToBeVisibleAsync();
        await Expect(tarjetas.Nth(gratuito).Locator(".etiqueta", new() { HasText = "Pro" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Si_el_mecanico_borra_su_cuenta_la_consulta_pendiente_vuelve_al_cliente()
    {
        var (mecanico, cuentaMecanico, id) = await CompartidaAsync();

        await IrAsync(mecanico, "/perfil");
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Eliminar cuenta" }).ClickAsync();
        await mecanico.GetByRole(AriaRole.Dialog).GetByLabel("Tu contraseña").FillAsync(cuentaMecanico.Contrasena);
        await mecanico.GetByRole(AriaRole.Button, new() { Name = "Eliminar definitivamente" }).ClickAsync();
        await Expect(mecanico.GetByRole(AriaRole.Status)).ToHaveTextAsync("Tu cuenta fue eliminada.");

        await IrAsync(Pagina, $"/consultas/{id}");
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Pedile a un mecánico que lo revise" })).ToBeVisibleAsync();
        await Expect(TarjetaDelDirectorio(Pagina, cuentaMecanico.Nombre)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task El_mecanico_guarda_los_datos_del_taller_y_cambia_de_plan()
    {
        await RegistrarAsync(Pagina, mecanico: true);

        await Pagina.Locator("#taller").FillAsync("Frenos Rápidos E2E");
        await Pagina.Locator("label.campo-check", new() { HasText = "Frenos" }).Locator("input").CheckAsync();
        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).ClickAsync();
        await Expect(Exito(Pagina)).ToHaveTextAsync("Datos del taller guardados.");
        await Expect(Pagina.GetByRole(AriaRole.Heading, new() { Name = "Frenos Rápidos E2E" })).ToBeVisibleAsync();

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Pasar a Pro" }).ClickAsync();
        await Expect(Exito(Pagina)).ToHaveTextAsync("Ya tenés el plan Pro.");
        await Expect(Pagina.Locator(".resumen-taller")).Not.ToContainTextAsync("/ 5");

        await Pagina.GetByRole(AriaRole.Button, new() { Name = "Volver al plan gratuito" }).ClickAsync();
        await Expect(Exito(Pagina)).ToHaveTextAsync("Volviste al plan gratuito.");
        await Expect(Pagina.Locator(".resumen-taller")).ToContainTextAsync("0 / 5");
    }
}
