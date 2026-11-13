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
        using (ContextoCorrelacion.Establecer(correlationId))
        {
            await siguiente(contexto);
        }
    }
}

/// <summary>
/// El correlation id de la operacion en curso, para el codigo que no ve el HttpContext (la capa
/// de aplicacion cuando publica un mensaje a la cola). Fluye con la ejecucion asincronica, igual
/// que el contexto de log de Serilog: lo que se loguea y lo que se publica llevan el mismo id.
/// </summary>
public static class ContextoCorrelacion
{
    private static readonly AsyncLocal<string?> _actual = new();

    public static string? Actual => _actual.Value;

    /// <summary>Lo fija hasta que se descarte el valor devuelto. Lo usan el middleware y el
    /// consumidor de la cola, que recibe el id en el mensaje.</summary>
    public static IDisposable Establecer(string? correlationId)
    {
        var anterior = _actual.Value;
        _actual.Value = correlationId;
        return new Restaurar(anterior);
    }

    private sealed class Restaurar(string? anterior) : IDisposable
    {
        public void Dispose() => _actual.Value = anterior;
    }
}

public static class CorrelacionExtensions
{
    public static IApplicationBuilder UsarCorrelacion(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelacionMiddleware>();
}
