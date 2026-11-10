using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Infraestructura;

/// <summary>Una cuenta creada por la prueba, con lo necesario para volver a entrar.</summary>
public sealed record Cuenta(string Nombre, string Email, string Contrasena);

/// <summary>
/// Base de todas las pruebas de punta a punta. Cada prueba recibe un contexto de navegador nuevo
/// (sesion vacia), graba una traza de Playwright y, al terminar, deja captura y traza en
/// <see cref="Entorno.DirectorioResultados"/>. Ademas falla si la Web mostro el cartel
/// "Ocurrio un error inesperado": una excepcion no atrapada en el servidor es un error aunque la
/// prueba haya llegado al final.
/// </summary>
[Collection(ColeccionE2E.Nombre)]
public abstract partial class PruebaE2E(NavegadorFixture fixture, ITestOutputHelper salida) : IAsyncLifetime
{
    public const string Vehiculo = "Volkswagen Gol 2015 (1.6)";
    private readonly List<IBrowserContext> _contextos = [];

    protected IPage Pagina { get; private set; } = null!;

    protected ITestOutputHelper Salida => salida;

    /// <summary>Las pruebas que esperan un error no atrapado lo apagan.</summary>
    protected bool VerificarErrorNoAtrapado { get; set; } = true;

    public async Task InitializeAsync() => Pagina = await NuevaPaginaAsync();

    /// <summary>Otra persona usando la app al mismo tiempo: contexto y sesion propios.</summary>
    protected async Task<IPage> NuevaPaginaAsync(ViewportSize? pantalla = null)
    {
        var contexto = await fixture.Navegador.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Entorno.UrlWeb,
            Locale = "es-AR",
            ViewportSize = pantalla ?? new ViewportSize { Width = 1280, Height = 900 }
        });
        await contexto.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        _contextos.Add(contexto);
        return await contexto.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        var nombre = NombreArchivo();
        var errores = new List<string>();

        for (var i = 0; i < _contextos.Count; i++)
        {
            var contexto = _contextos[i];
            var sufijo = _contextos.Count > 1 ? $"-{i + 1}" : "";
            foreach (var pagina in contexto.Pages)
            {
                try
                {
                    await pagina.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = Path.Combine(Entorno.DirectorioResultados, "capturas", $"{nombre}{sufijo}.png"),
                        FullPage = true
                    });

                    if (VerificarErrorNoAtrapado && await pagina.Locator("#blazor-error-ui").IsVisibleAsync())
                        errores.Add($"La pagina {pagina.Url} mostro 'Ocurrio un error inesperado' (excepcion no atrapada en la Web).");
                }
                catch (PlaywrightException)
                {
                    // La pagina ya estaba cerrada: no hay nada que capturar.
                }
            }

            await contexto.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(Entorno.DirectorioResultados, "trazas", $"{nombre}{sufijo}.zip")
            });
            await contexto.CloseAsync();
        }

        if (errores.Count > 0)
            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, errores));
    }

    // ── Navegacion ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Abre una ruta y espera a que Blazor este interactivo. MainLayout muestra "Cargando…" hasta
    /// restaurar la sesion, y eso pasa recien con el circuito conectado: si se hace clic antes, el
    /// evento se pierde y la prueba falla al azar.
    /// </summary>
    protected static async Task IrAsync(IPage pagina, string ruta)
    {
        await pagina.GotoAsync(ruta);
        await EsperarListaAsync(pagina);
    }

    protected Task IrAsync(string ruta) => IrAsync(Pagina, ruta);

    protected static async Task EsperarListaAsync(IPage pagina) =>
        await Expect(pagina.Locator("p.vacio", new() { HasText = "Cargando…" })).ToHaveCountAsync(0);

    // ── Cuentas ─────────────────────────────────────────────────────────────────

    protected static string Unico() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Crea una cuenta por la pantalla de registro y deja la sesion iniciada.</summary>
    protected static async Task<Cuenta> RegistrarAsync(IPage pagina, bool mecanico = false, string? nombre = null)
    {
        var id = Unico();
        var cuenta = new Cuenta(
            nombre ?? (mecanico ? $"Mecanico E2E {id}" : $"Cliente E2E {id}"),
            $"e2e-{id}@ejemplo.com",
            "Clave-e2e-123");

        await IrAsync(pagina, "/login");
        await pagina.GetByText("Creá una").ClickAsync();
        await pagina.Locator("#nombre").FillAsync(cuenta.Nombre);
        await pagina.Locator("#email").FillAsync(cuenta.Email);
        await pagina.Locator("#contrasena").FillAsync(cuenta.Contrasena);
        await pagina.Locator("#repetirContrasena").FillAsync(cuenta.Contrasena);
        await pagina.Locator("label.opcion-rol", new() { HasText = mecanico ? "Soy mecánico" : "Para mi auto" }).ClickAsync();
        await pagina.Locator("button.btn-full").ClickAsync();

        await Expect(pagina).ToHaveURLAsync(new Regex(mecanico ? "/taller$" : "/$"));
        await Expect(pagina.Locator(".user-nombre")).ToHaveTextAsync(cuenta.Nombre);
        return cuenta;
    }

    protected static async Task IngresarAsync(IPage pagina, string email, string contrasena)
    {
        await IrAsync(pagina, "/login");
        await pagina.Locator("#email").FillAsync(email);
        await pagina.Locator("#contrasena").FillAsync(contrasena);
        await pagina.Locator("button.btn-full").ClickAsync();
        await Expect(pagina.Locator(".user-trigger")).ToBeVisibleAsync();
    }

    protected static async Task SalirAsync(IPage pagina)
    {
        await pagina.Locator(".user-trigger").ClickAsync();
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).First.ClickAsync();
        await Expect(pagina.Locator(".btn-ingresar")).ToBeVisibleAsync();
    }

    /// <summary>Cliente nuevo con un Gol 2015 1.6 en el garage: el vehiculo con mas piezas en la semilla.</summary>
    protected static async Task<Cuenta> ClienteConGolAsync(IPage pagina)
    {
        var cuenta = await RegistrarAsync(pagina);
        await AgregarGolAsync(pagina);
        return cuenta;
    }

    protected static Task AgregarGolAsync(IPage pagina, string? apodo = null) =>
        AgregarVehiculoAsync(pagina, apodo);

    /// <summary>El garage no admite dos veces el mismo vehiculo: para un segundo auto, otro modelo.</summary>
    protected static async Task AgregarVehiculoAsync(
        IPage pagina, string? apodo = null,
        string marca = "Volkswagen", string modelo = "Gol", string anio = "2015", string motor = "1.6")
    {
        await IrAsync(pagina, "/clientes");
        await ElegirVehiculoAsync(pagina, "garage", marca, modelo, anio, motor);
        if (apodo is not null)
            await pagina.Locator("#valias").FillAsync(apodo);
        await pagina.GetByRole(AriaRole.Button, new() { Name = "Agregar vehículo" }).ClickAsync();
        await Expect(pagina.GetByRole(AriaRole.Status)).ToContainTextAsync("quedó en tu garage");
    }

    // ── Formularios ─────────────────────────────────────────────────────────────

    /// <summary>Completa los cuatro desplegables de SelectorVehiculo. Cada uno se habilita
    /// cuando el anterior vuelve del servidor; SelectOption espera solo.</summary>
    protected static async Task ElegirVehiculoAsync(
        IPage pagina, string prefijo,
        string marca = "Volkswagen", string modelo = "Gol", string anio = "2015", string motor = "1.6")
    {
        await pagina.Locator($"#{prefijo}-marca").SelectOptionAsync(marca);
        await pagina.Locator($"#{prefijo}-modelo").SelectOptionAsync(modelo);
        await pagina.Locator($"#{prefijo}-anio").SelectOptionAsync(anio);
        await pagina.Locator($"#{prefijo}-motor").SelectOptionAsync(motor);
    }

    protected static ILocator BotonDiagnosticar(IPage pagina) =>
        pagina.GetByRole(AriaRole.Button, new() { Name = "Diagnosticar con IA" });

    protected static ILocator Consultas(IPage pagina) => pagina.Locator("ul.recientes > li");

    protected static ILocator Piezas(IPage pagina) => pagina.Locator(".piezas article.pieza");

    protected static ILocator TarjetaIA(IPage pagina) => pagina.Locator("section.bloque-ia");

    /// <summary>Busqueda sin sesion desde la portada, con el Gol.</summary>
    protected static async Task BuscarSinSesionAsync(IPage pagina, string texto)
    {
        await IrAsync(pagina, "/");
        await ElegirVehiculoAsync(pagina, "anon");
        await pagina.Locator("#textoAnon").FillAsync(texto);
        await BotonDiagnosticar(pagina).ClickAsync();
        await Expect(pagina.Locator("p.conteo")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Busqueda con sesion desde la portada. Devuelve el id de la consulta creada, leido del
    /// enlace de "Tus consultas": la lista viene de la base, asi que tambien prueba que se guardo.
    /// </summary>
    protected static async Task<Guid> BuscarConSesionAsync(IPage pagina, string texto)
    {
        if (!Uri.TryCreate(pagina.Url, UriKind.Absolute, out var actual) || actual.AbsolutePath != "/")
            await IrAsync(pagina, "/");

        var antes = await Consultas(pagina).CountAsync();
        await pagina.Locator("#texto").FillAsync(texto);
        await BotonDiagnosticar(pagina).ClickAsync();
        await Expect(Consultas(pagina)).ToHaveCountAsync(antes + 1);

        var href = await Consultas(pagina).First.Locator("a.link-discreto").GetAttributeAsync("href");
        return Guid.Parse(href!.Split('/').Last());
    }

    /// <summary>Desde la pagina de la consulta, el duenio la comparte con un mecanico del directorio.</summary>
    protected static async Task CompartirAsync(IPage pagina, Guid consultaId, string nombreMecanico)
    {
        await IrAsync(pagina, $"/consultas/{consultaId}");
        await TarjetaDelDirectorio(pagina, nombreMecanico)
            .GetByRole(AriaRole.Button, new() { Name = "Compartir" }).ClickAsync();
    }

    protected static ILocator TarjetaDelDirectorio(IPage pagina, string nombreMecanico) =>
        pagina.Locator(".directorio .vehiculo-card").Filter(new() { HasText = nombreMecanico });

    protected static ILocator Exito(IPage pagina) => pagina.Locator(".alerta-exito");

    protected static ILocator Error(IPage pagina) => pagina.Locator(".alerta-error, .error-campo");

    // ── Archivos de resultado ───────────────────────────────────────────────────

    /// <summary>"Clase.Metodo" de la prueba en curso, para nombrar captura y traza. xUnit 2 no lo
    /// expone en la API publica; se lee del ITestOutputHelper y, si cambia, se usa un id.</summary>
    private string NombreArchivo()
    {
        var test = salida.GetType()
            .GetField("test", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(salida) as ITest;
        var nombre = test?.DisplayName ?? $"{GetType().Name}-{Unico()}";
        nombre = nombre.Replace("RepMatch.E2E.Pruebas.", "");
        return CaracteresInvalidos().Replace(nombre, "_");
    }

    [GeneratedRegex(@"[^\w\.\-]+")]
    private static partial Regex CaracteresInvalidos();
}
