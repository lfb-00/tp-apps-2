using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace RepMatch.Common.Logging;

/// <summary>
/// Propaga un identificador de correlacion entre servicios. Con dos procesos (Web y Catalogo.Api)
/// ya hace falta para poder seguir una misma operacion en dos archivos de log distintos; cuando
/// en la Segunda Parte se sumen la cola y los adapters externos, es lo unico que permite
/// reconstruir el recorrido completo de una busqueda.
/// </summary>
public sealed class CorrelacionMiddleware(RequestDelegate siguiente)
{
    public const string NombreCabecera = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var correlationId = contexto.Request.Headers[NombreCabecera].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N")[..12];

        contexto.Items[NombreCabecera] = correlationId;
        contexto.Response.Headers[NombreCabecera] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await siguiente(contexto);
        }
    }
}

public static class CorrelacionExtensions
{
    public static IApplicationBuilder UsarCorrelacion(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelacionMiddleware>();
}
