using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace RepMatch.Common.Logging;

/// <summary>
/// Logging estructurado unificado para todos los servicios de RepMatch. Centralizarlo aca evita
/// que cada host repita la configuracion y garantiza que las evidencias del TP salgan con el
/// mismo formato desde la Web y desde la API.
/// </summary>
public static class ConfiguracionSerilog
{
    /// <summary>
    /// Escribe a consola y a un archivo diario bajo logs/. El nivel y el destino salen de
    /// appsettings/variables de entorno, nunca hardcodeados (configuracion externalizada).
    /// </summary>
    public static void UsarSerilog(this WebApplicationBuilder builder, string nombreServicio)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreServicio);

        var rutaLogs = builder.Configuration["Logging:RutaArchivo"]
            ?? Path.Combine(AppContext.BaseDirectory, "logs", $"{nombreServicio}-.log");

        builder.Host.UseSerilog((contexto, servicios, cfg) => cfg
            .ReadFrom.Configuration(contexto.Configuration)
            .ReadFrom.Services(servicios)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Servicio", nombreServicio)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Servicio}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(rutaLogs,
                rollingInterval: RollingInterval.Day,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{Servicio}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));
    }
}
