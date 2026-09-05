using RepMatch.Common.Logging;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Configuracion;
using RepMatch.Persistence;
using RepMatch.Web.Components;
using RepMatch.Web.Servicios;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UsarSerilog("Web");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registra la capa de datos, la de logica de negocio y las dos formas de alcanzar el catalogo.
builder.Services.AgregarCatalogo(builder.Configuration);

var app = builder.Build();

app.UsarCorrelacion();
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.Web",
    estado = "ok",
    utc = DateTimeOffset.UtcNow
}));

// Ejercita el catalogo por el camino que indique la configuracion y devuelve modo y latencia.
// Existe para que scripts/evidencias.sh pueda generar las evidencias de ejecucion local y remota
// sin depender de que alguien haga clic en la interfaz.
app.MapGet("/evidencia/catalogo", async (
    ICatalogoRepuestos catalogo,
    string? marca, string? modelo, int? anio, string? motor,
    CancellationToken ct) =>
{
    var vehiculo = new VehiculoDto
    {
        Marca = marca ?? "Volkswagen",
        Modelo = modelo ?? "Gol",
        Anio = anio ?? 2015,
        Motor = motor ?? "1.6"
    };

    var medicion = await Cronometro.MedirAsync(catalogo.Modo,
        token => catalogo.BuscarCompatiblesAsync(vehiculo, null, token), ct);

    Log.Information("Evidencia acceso {Medicion} vehiculo={Vehiculo} resultados={Cantidad}",
        medicion, vehiculo, medicion.Valor.Count);

    return Results.Ok(new
    {
        modo = medicion.Origen,
        vehiculo = vehiculo.ToString(),
        milisegundos = Math.Round(medicion.MilisegundosTranscurridos, 1),
        cantidad = medicion.Valor.Count,
        codigos = medicion.Valor.Select(r => r.CodigoCanonico)
    });
});

await app.Services.InicializarBaseAsync();

var opciones = app.Services.GetRequiredService<OpcionesCatalogo>();

try
{
    Log.Information("Web iniciada. Modo de acceso al catalogo: {Modo} ({Url})",
        opciones.Modo,
        opciones.Modo == ModoAccesoCatalogo.Remoto ? opciones.UrlBaseRemota : "en proceso");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La Web termino de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
