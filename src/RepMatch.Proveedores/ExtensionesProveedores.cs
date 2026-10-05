using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Contracts;
using RepMatch.Proveedores.Configuracion;
using RepMatch.Proveedores.eBay;
using RepMatch.Proveedores.MercadoLibre;

namespace RepMatch.Proveedores;

/// <summary>
/// Registro de los proveedores de ofertas externas. Se llama desde el host (RepMatch.Web)
/// despues de AgregarAplicacion. Cada proveedor usa un HttpClient tipado gestionado por
/// IHttpClientFactory para evitar el agotamiento de sockets.
/// </summary>
public static class ExtensionesProveedores
{
    public static IServiceCollection AgregarProveedores(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        servicios.Configure<OpcionesProveedores>(
            configuracion.GetSection(OpcionesProveedores.Seccion));

        // IMemoryCache para el token OAuth de eBay; si ya fue registrado por el host, no duplica.
        servicios.AddMemoryCache();

        servicios.AddHttpClient<MercadoLibreProveedor>(c =>
        {
            c.BaseAddress = new Uri("https://api.mercadolibre.com/");
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        // Usa la factory del cliente tipado para que HttpClient reciba la configuracion correcta.
        // AddScoped<IInterface, TImpl>() no usa la factory de AddHttpClient — falla al resolver HttpClient.
        servicios.AddScoped<IProveedorOfertas>(sp => sp.GetRequiredService<MercadoLibreProveedor>());

        servicios.AddHttpClient<EbayProveedor>(c =>
        {
            c.BaseAddress = new Uri("https://api.ebay.com/");
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        servicios.AddScoped<IProveedorOfertas>(sp => sp.GetRequiredService<EbayProveedor>());

        return servicios;
    }
}
