using RepMatch.Aplicacion;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Common.Logging;
using RepMatch.Contracts;
using RepMatch.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UsarSerilog("Catalogo.Api");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Capa de datos y capa de logica de negocio, ambas registradas desde su propio componente.
builder.Services.AgregarPersistencia(builder.Configuration);
builder.Services.AgregarAplicacion();

// Este servicio ES el duenio del catalogo: siempre resuelve en proceso.
builder.Services.AddScoped<ICatalogoRepuestos, CatalogoLocal>();

var app = builder.Build();

app.UsarCorrelacion();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.Catalogo.Api",
    estado = "ok",
    utc = DateTimeOffset.UtcNow
}));

await app.Services.InicializarBaseAsync();

try
{
    Log.Information("Catalogo.Api escuchando");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Catalogo.Api termino de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesta para que las pruebas de integracion puedan instanciar el host con WebApplicationFactory.</summary>
public partial class Program;
