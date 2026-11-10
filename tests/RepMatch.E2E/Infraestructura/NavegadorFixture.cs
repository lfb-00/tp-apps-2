using Microsoft.Playwright;

namespace RepMatch.E2E.Infraestructura;

/// <summary>
/// Un solo Chromium para toda la corrida: abrirlo cuesta segundos, un contexto nuevo cuesta
/// milisegundos. Cada prueba trabaja en su propio contexto (cookies y sessionStorage propios),
/// asi que las sesiones no se pisan aunque compartan el navegador.
/// </summary>
public sealed class NavegadorFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Navegador { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Blazor Server re-renderiza por SignalR: 10 s cubre una busqueda con IA remota lenta sin
        // esconder una pantalla colgada.
        Assertions.SetDefaultExpectTimeout(10_000);

        // Descarga Chromium si falta (la primera vez en una maquina). Si ya esta, vuelve enseguida.
        // Asi nadie tiene que acordarse de correr "playwright install" antes de las pruebas.
        var codigo = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (codigo != 0)
            throw new InvalidOperationException(
                $"No se pudo instalar Chromium para Playwright (codigo {codigo}). Revisar la conexion " +
                "a internet o instalarlo a mano con: pwsh bin/<config>/net10.0/playwright.ps1 install chromium");

        _playwright = await Playwright.CreateAsync();
        Navegador = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !Entorno.ConVentana,
            SlowMo = Entorno.ConVentana ? 150 : 0
        });
    }

    public async Task DisposeAsync()
    {
        if (Navegador is not null)
            await Navegador.CloseAsync();
        _playwright?.Dispose();
    }
}

/// <summary>
/// Todas las clases de prueba en una sola coleccion: corren en serie. Comparten la base de la
/// Web, y en paralelo una prueba que cambia el plan de un mecanico de la semilla podria romper
/// otra. Con cuentas nuevas por prueba el costo es solo tiempo.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionE2E : ICollectionFixture<NavegadorFixture>
{
    public const string Nombre = "E2E";
}
