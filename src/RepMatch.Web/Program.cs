using RepMatch.Common.Logging;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Configuracion;
using RepMatch.Fuentes.Vtex;
using RepMatch.Mensajeria;
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
builder.Services.AgregarDiagnostico(builder.Configuration);
// Tiendas donde buscar precios (API publica de VTEX). Cada una sale de la seccion "Ofertas".
builder.Services.AgregarFuentesOfertas(builder.Configuration);
// Cola de mensajes: con Mensajeria:Habilitada los eventos de dominio se publican en RabbitMQ.
builder.Services.AgregarMensajeria(builder.Configuration);
builder.Services.AgregarProteccionDatos(builder.Configuration);
builder.Services.AddScoped<RepMatch.Web.Servicios.SesionActual>();
builder.Services.AddScoped<RepMatch.Web.Servicios.TemaActual>();

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
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.Web",
    estado = "ok",
    utc = DateTimeOffset.UtcNow
}));

// "Ir a la tienda": registra la derivacion (la base de la comision de afiliado) y redirige al sitio
// de origen. Pasa por el servidor y no por un link directo para que la salida quede registrada
// aunque el navegador no ejecute JavaScript.
app.MapGet("/derivar/{ofertaId:guid}", async (
    Guid ofertaId, RepMatch.Aplicacion.Fachada.FachadaAplicacion fachada, CancellationToken ct) =>
{
    var resultado = await fachada.DerivarAsync(ofertaId, ct);
    return resultado.EsExitoso
        ? Results.Redirect(resultado.Valor)
        : Results.NotFound(new { error = resultado.Error });
});

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
var opcionesIA = app.Services.GetRequiredService<OpcionesIA>();

try
{
    Log.Information("Web iniciada. Modo de acceso al catalogo: {Modo} ({Url})",
        opciones.Modo,
        opciones.Modo switch
        {
            ModoAccesoCatalogo.Remoto => opciones.UrlBaseRemota,
            ModoAccesoCatalogo.Soap => $"repuestos por SOAP en {opciones.UrlSoap}, vehiculos por REST en {opciones.UrlBaseRemota}",
            _ => "en proceso"
        });
    Log.Information("Modo de acceso a la IA: {Modo} ({Url})",
        opcionesIA.Modo,
        opcionesIA.Modo == ModoAccesoIA.Remoto ? opcionesIA.UrlBaseRemota : "en proceso");
    var tiendas = app.Services.GetServices<IFuenteOfertas>().Select(f => f.Nombre).ToList();
    Log.Information("Tiendas para precios: {Tiendas}",
        tiendas.Count == 0 ? "ninguna (la busqueda muestra piezas sin precios)" : string.Join(", ", tiendas));

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
