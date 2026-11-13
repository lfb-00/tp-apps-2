using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Clientes.Soap;

public static class ExtensionesClienteSoap
{
    /// <summary>
    /// Registra <see cref="CatalogoSoap"/> contra Catalogo.Soap. Como AgregarCatalogoRemoto, registra
    /// el tipo concreto y no la interfaz: la decision de cual queda enlazada es de la fabrica del host.
    /// </summary>
    public static IServiceCollection AgregarCatalogoSoap(this IServiceCollection servicios, OpcionesCatalogo opciones)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(opciones);

        servicios.AddHttpContextAccessor();
        servicios.AddSingleton(sp =>
        {
            var contexto = sp.GetRequiredService<IHttpContextAccessor>();
            return new FabricaCanalSoap(
                opciones.UrlSoap,
                TimeSpan.FromSeconds(opciones.TimeoutSegundos),
                interno => new CorrelacionSoapHandler(contexto, interno));
        });
        servicios.AddScoped<CatalogoSoap>();

        return servicios;
    }
}
