using RepMatch.Common.Logging;
using RepMatch.Contracts;
using RepMatch.IA;
using RepMatch.IA.Llm;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UsarSerilog("IA.Api");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Este servicio ES el componente de IA: siempre diagnostica en proceso.
builder.Services.AgregarDiagnosticoIA(builder.Configuration);
builder.Services.AddScoped<IDiagnosticador>(sp => sp.GetRequiredService<DiagnosticadorConRespaldo>());

var app = builder.Build();

app.UsarCorrelacion();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

var llm = app.Services.GetRequiredService<OpcionesLlm>();

// Dice si el LLM esta configurado, nunca la clave: en la demo se ve de un vistazo que fuente va a responder.
app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.IA.Api",
    estado = "ok",
    llm = llm.EstaConfigurado ? llm.Modelo : "sin clave: responde el clasificador local",
    utc = DateTimeOffset.UtcNow
}));

try
{
    Log.Information("IA.Api escuchando. LLM: {Llm}",
        llm.EstaConfigurado ? $"{llm.Modelo} en {llm.UrlBase}" : "sin clave, solo clasificador local");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IA.Api termino de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesta para que las pruebas de integracion puedan instanciar el host con WebApplicationFactory.</summary>
public partial class Program;
