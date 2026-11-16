using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RepMatch.Common.Logging;

public static class ExtensionesProblemDetails
{
    /// <summary>
    /// ProblemDetails (RFC 9457) igual en todos los hosts REST, con el correlation id en el cuerpo:
    /// quien recibe un error puede reportarlo citando ese id, que es el mismo que buscar en los logs
    /// de todos los servicios. Tambien vuelve a poner la cabecera X-Correlation-Id, porque el
    /// manejador de excepciones limpia la respuesta (y sus cabeceras) antes de escribir el 500.
    /// Nunca agrega detalles de la excepcion: eso queda en el log, no en la respuesta.
    /// </summary>
    public static IServiceCollection AgregarProblemDetails(this IServiceCollection servicios) =>
        servicios.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
        {
            if (contexto.HttpContext.Items[CorrelacionMiddleware.NombreCabecera] is not string id)
                return;

            contexto.ProblemDetails.Extensions["correlationId"] = id;
            contexto.HttpContext.Response.Headers[CorrelacionMiddleware.NombreCabecera] = id;
        });
}
