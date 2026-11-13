using RepMatch.Aplicacion.Servicios;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Configuracion;
using RepMatch.Mensajeria;
using RepMatch.Notificaciones.Worker.Consumidores;
using RepMatch.Persistence;
using Serilog;

// Worker de la cola (Segunda Parte, issue #14): consume los eventos que publican la Web y
// Catalogo.Api. Es un host web solo para tener /salud (el healthcheck de Docker) y una consulta
// REST del registro de comisiones; el trabajo lo hacen los consumidores en segundo plano.

var builder = WebApplication.CreateBuilder(args);

builder.UsarSerilog("Worker");

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// La misma base que el resto. El worker no crea el esquema ni siembra datos: eso lo hacen los
// hosts que arrancan antes (en compose depende de catalogo-api sano).
builder.Services.AgregarPersistencia(builder.Configuration);
builder.Services.AddScoped<ServicioComisiones>();

builder.Services.AgregarMensajeria(builder.Configuration);
builder.Services.AgregarConsumidor<ConsumidorComisiones>();
builder.Services.AgregarConsumidor<ConsumidorActividad>();

var app = builder.Build();

app.UsarCorrelacion();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var opciones = app.Services.GetRequiredService<OpcionesMensajeria>();

app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.Notificaciones.Worker",
    estado = "ok",
    broker = opciones.Habilitada ? $"{opciones.Host}:{opciones.Puerto}" : "mensajeria desactivada",
    colas = new[] { ConsumidorComisiones.Definicion.Nombre, ConsumidorActividad.Definicion.Nombre },
    utc = DateTimeOffset.UtcNow
}));

// Lo que fue sumando el consumidor de derivacion.tienda: muestra que el mensaje llego y se proceso.
app.MapGet("/api/comisiones", async (ServicioComisiones comisiones, CancellationToken ct) =>
        Results.Ok(await comisiones.ListarAsync(ct)))
    .WithName("ListarComisiones")
    .WithSummary("Derivaciones por tienda y mes, registradas desde la cola.");

try
{
    Log.Information("Worker iniciado. Broker: {Broker}",
        opciones.Habilitada ? $"{opciones.Host}:{opciones.Puerto}" : "mensajeria desactivada, los consumidores esperan");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El worker termino de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesta para que las pruebas puedan instanciar el host con WebApplicationFactory.</summary>
public partial class Program;
