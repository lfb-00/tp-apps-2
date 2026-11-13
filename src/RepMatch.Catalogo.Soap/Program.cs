using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Catalogo.Soap;
using RepMatch.Catalogo.Soap.Contrato;
using RepMatch.Common.Logging;
using RepMatch.Contracts;
using RepMatch.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UsarSerilog("Catalogo.Soap");

// Solo lo que el servicio usa: la capa de datos y el catalogo en proceso, como Catalogo.Api.
// No registra la fachada ni la IA: este host responde consultas de compatibilidad y nada mas.
builder.Services.AgregarPersistencia(builder.Configuration);
builder.Services.AddScoped<ICatalogoRepuestos, CatalogoLocal>();

// CoreWCF: el lado servidor de WCF para ASP.NET Core (en .NET moderno WCF solo existe como cliente).
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
// El WSDL publica la direccion con el host con que se lo pidio (localhost:8082, catalogo-soap:8080
// dentro de compose), no la interna del contenedor: si no, un cliente generado apuntaria a otro lado.
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddTransient<ServicioConsultaCompatibilidad>();

var app = builder.Build();

app.UsarCorrelacion();
app.UseSerilogRequestLogging();

app.UseServiceModel(servicios =>
{
    servicios.AddService<ServicioConsultaCompatibilidad>(opciones =>
    {
        // Con Kestrel, CoreWCF toma la direccion base de las que escucha el servidor. Un host sin
        // direcciones, como el TestServer de las pruebas, la tiene que recibir por configuracion.
        var direccionBase = app.Configuration["Soap:DireccionBase"];
        if (!string.IsNullOrWhiteSpace(direccionBase))
            opciones.BaseAddresses.Add(new Uri(direccionBase));
    });
    servicios.AddServiceEndpoint<ServicioConsultaCompatibilidad, IConsultaCompatibilidad>(
        // Con el namespace del contrato, el WSDL sale en un solo documento y no en partes con tempuri.org.
        new BasicHttpBinding { Namespace = Espacio.Nombre }, RutasSoap.Servicio);

    // GET <servicio>?wsdl devuelve el contrato. Es parte del entregable.
    app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;
});

app.MapGet("/salud", () => Results.Ok(new
{
    servicio = "RepMatch.Catalogo.Soap",
    estado = "ok",
    wsdl = $"{RutasSoap.Servicio}?wsdl",
    utc = DateTimeOffset.UtcNow
}));

await app.Services.InicializarBaseAsync();

try
{
    Log.Information("Catalogo.Soap escuchando. WSDL en {Ruta}?wsdl", RutasSoap.Servicio);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Catalogo.Soap termino de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesta para que las pruebas de integracion puedan instanciar el host con WebApplicationFactory.</summary>
public partial class Program;

namespace RepMatch.Catalogo.Soap
{
    public static class RutasSoap
    {
        /// <summary>Basic HTTP (SOAP 1.1): el binding que entiende cualquier cliente SOAP, no solo .NET.</summary>
        public const string Servicio = "/ConsultaCompatibilidad.svc";
    }
}
