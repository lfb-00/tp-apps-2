using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Fuentes.Vtex;

public static class ExtensionesFuentesVtex
{
    /// <summary>
    /// Lee la seccion "Ofertas" y registra un <see cref="FuenteVtex"/> por cada tienda habilitada,
    /// con su propio HttpClient (URL base, timeout, User-Agent). Todas quedan como
    /// <see cref="IFuenteOfertas"/>: <c>ServicioOfertas</c> recibe la lista completa y no sabe
    /// cuantas ni cuales son.
    /// </summary>
    public static IServiceCollection AgregarFuentesOfertas(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesOfertas.Seccion).Get<OpcionesOfertas>() ?? new OpcionesOfertas();
        servicios.RemoveAll<OpcionesOfertas>();
        servicios.AddSingleton(opciones);
        servicios.AddMemoryCache();

        foreach (var tienda in opciones.Tiendas.Where(t => t.Habilitada && !string.IsNullOrWhiteSpace(t.UrlBase)))
        {
            servicios.AddHttpClient(FuenteVtex.NombreCliente(tienda), http =>
            {
                http.BaseAddress = new Uri(tienda.UrlBase.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(tienda.TimeoutSegundos);
                // Generico a proposito: identifica al cliente sin datos de quien lo opera.
                http.DefaultRequestHeaders.Add("User-Agent", "RepMatch/1.0 (comparador de repuestos)");
                http.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            servicios.AddSingleton<IFuenteOfertas>(sp => new FuenteVtex(
                tienda,
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<FuenteVtex>>()));
        }

        return servicios;
    }
}
