using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Fuentes.Mapas;

public static class ExtensionesMapas
{
    /// <summary>
    /// Registra OpenStreetMap como <see cref="IMapaCasasRepuestos"/> si "Mapas:Habilitado" es true
    /// (el valor por defecto). Si no, queda el mapa desactivado que
    /// registra la capa de aplicacion y la pantalla no ofrece buscar comercios. En la Web va despues
    /// de <c>AgregarFuentesOfertas</c>, para usar la misma cache distribuida.
    /// </summary>
    public static IServiceCollection AgregarMapas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesMapas.Seccion).Get<OpcionesMapas>() ?? new OpcionesMapas();
        if (!opciones.Habilitado)
            return servicios;

        // El worker no registra cache distribuida (la Web si, con las tiendas): una en memoria alcanza
        // para las zonas ya ubicadas.
        if (!servicios.Any(d => d.ServiceType == typeof(IDistributedCache)))
            servicios.AddDistributedMemoryCache();

        void Configurar(HttpClient http, string url)
        {
            http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            http.Timeout = Timeout.InfiniteTimeSpan;   // lo maneja Polly
            http.DefaultRequestHeaders.Add("User-Agent", opciones.AgenteUsuario);
            http.DefaultRequestHeaders.Add("Accept-Language", "es");
        }

        servicios.AddHttpClient(MapaOpenStreetMap.ClienteOverpass, http => Configurar(http, opciones.UrlOverpass));
        servicios.AddHttpClient(MapaOpenStreetMap.ClienteNominatim, http => Configurar(http, opciones.UrlNominatim));

        servicios.AddSingleton<IMapaCasasRepuestos>(sp => new MapaOpenStreetMap(
            opciones,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<ILogger<MapaOpenStreetMap>>()));
        servicios.AddSingleton(opciones);

        return servicios;
    }
}
