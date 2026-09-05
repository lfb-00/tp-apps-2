using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Clientes.Rest;

public static class ExtensionesClienteRest
{
    /// <summary>
    /// Registra <see cref="CatalogoRemoto"/> como HttpClient tipado, propagando el identificador
    /// de correlacion para poder cruzar los logs de los dos procesos.
    ///
    /// Registra el tipo CONCRETO, no la interfaz: el host enlaza ICatalogoRepuestos a esta
    /// implementacion o a la local segun configuracion, y la pagina de evidencias necesita poder
    /// pedir las dos a la vez para compararlas.
    /// </summary>
    public static IServiceCollection AgregarCatalogoRemoto(
        this IServiceCollection servicios, OpcionesCatalogo opciones)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(opciones);

        servicios.AddHttpContextAccessor();
        servicios.AddTransient<PropagacionCorrelacionHandler>();

        servicios
            .AddHttpClient<CatalogoRemoto>(http =>
            {
                http.BaseAddress = new Uri(opciones.UrlBaseRemota.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos);
                http.DefaultRequestHeaders.Add("User-Agent", "RepMatch.Web");
            })
            .AddHttpMessageHandler<PropagacionCorrelacionHandler>();

        return servicios;
    }
}

/// <summary>Copia el X-Correlation-Id de la peticion entrante a la saliente.</summary>
public sealed class PropagacionCorrelacionHandler(IHttpContextAccessor contexto) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage peticion, CancellationToken ct)
    {
        var correlationId = contexto.HttpContext?.Items[CorrelacionMiddleware.NombreCabecera] as string;

        if (!string.IsNullOrWhiteSpace(correlationId)
            && !peticion.Headers.Contains(CorrelacionMiddleware.NombreCabecera))
        {
            peticion.Headers.Add(CorrelacionMiddleware.NombreCabecera, correlationId);
        }

        return base.SendAsync(peticion, ct);
    }
}
