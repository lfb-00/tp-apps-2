using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Fuentes.Ebay;

public static class ExtensionesFuenteEbay
{
    /// <summary>
    /// Registra eBay como una <see cref="IFuenteOfertas"/> mas, solo si "Ofertas:Ebay" tiene
    /// credenciales. Sin ellas no se registra nada: <c>ServicioOfertas</c> sigue con las tiendas
    /// VTEX, sin avisos de una tienda caida que nunca estuvo configurada. Va despues de
    /// <c>AgregarFuentesOfertas</c>, que registra la cache distribuida.
    /// </summary>
    public static IServiceCollection AgregarFuenteEbay(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection($"{OpcionesOfertas.Seccion}:Ebay").Get<OpcionesEbay>() ?? new OpcionesEbay();
        if (!opciones.EstaConfigurada)
            return servicios;

        servicios.AddHttpClient(FuenteEbay.NombreCliente, http =>
        {
            http.BaseAddress = new Uri(opciones.UrlBase.TrimEnd('/') + "/");
            http.Timeout = Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.Add("User-Agent", "RepMatch/1.0 (comparador de repuestos)");
            http.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        servicios.AddSingleton<IFuenteOfertas>(sp => new FuenteEbay(
            opciones,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<ILogger<FuenteEbay>>()));

        return servicios;
    }
}
