using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RepMatch.Common.Configuracion;

/// <summary>
/// Registro de configuracion externalizada con validacion al arranque. Si falta o esta mal una
/// clave, el servicio no levanta y dice cual: mucho mejor que descubrirlo con un
/// NullReferenceException en medio de la demo.
/// </summary>
public static class ExtensionesOpciones
{
    public static IServiceCollection AgregarOpcionesValidadas<TOpciones>(
        this IServiceCollection servicios, IConfiguration configuracion, string seccion)
        where TOpciones : class
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentException.ThrowIfNullOrWhiteSpace(seccion);

        servicios.AddOptions<TOpciones>()
            .Bind(configuracion.GetSection(seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return servicios;
    }
}
