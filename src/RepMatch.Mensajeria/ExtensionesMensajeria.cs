using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;

namespace RepMatch.Mensajeria;

public static class ExtensionesMensajeria
{
    /// <summary>
    /// Lee la seccion "Mensajeria". Con <c>Habilitada=true</c> enlaza <see cref="IEventBus"/> a
    /// <see cref="BusRabbitMq"/>; si no, deja el bus desactivado que registra la capa de aplicacion,
    /// y la aplicacion funciona sin broker. Es decision del host, como el catalogo y la IA.
    /// </summary>
    public static IServiceCollection AgregarMensajeria(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesMensajeria.Seccion).Get<OpcionesMensajeria>()
                       ?? new OpcionesMensajeria();
        servicios.RemoveAll<OpcionesMensajeria>();
        servicios.AddSingleton(opciones);

        if (!opciones.Habilitada)
            return servicios;

        servicios.TryAddSingleton<ConexionRabbitMq>();
        servicios.RemoveAll<IEventBus>();
        servicios.AddSingleton<IEventBus, BusRabbitMq>();
        return servicios;
    }

    /// <summary>Registra un consumidor como servicio en segundo plano. Requiere la mensajeria
    /// habilitada: un consumidor sin broker no tiene sentido.</summary>
    public static IServiceCollection AgregarConsumidor<TConsumidor>(this IServiceCollection servicios)
        where TConsumidor : class, Microsoft.Extensions.Hosting.IHostedService
    {
        servicios.TryAddSingleton<ConexionRabbitMq>();
        servicios.AddHostedService<TConsumidor>();
        return servicios;
    }
}
