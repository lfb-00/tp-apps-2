using RepMatch.Aplicacion;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Clientes.Rest;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Persistence;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Factory del componente de catalogo: decide en el arranque si <see cref="ICatalogoRepuestos"/>
/// se resuelve por invocacion directa en proceso (<see cref="CatalogoLocal"/>) o por HTTP contra
/// Catalogo.Api (<see cref="CatalogoRemoto"/>), leyendo la seccion "Catalogo" de la configuracion.
///
/// Es el punto exacto donde se materializa el ejercicio de acceso local vs. remoto del TP Inicial:
/// cambiando una sola clave de configuracion (o la variable de entorno Catalogo__Modo) toda la
/// aplicacion pasa de un modo al otro, sin recompilar y sin que ningun consumidor se entere.
///
/// Ademas registra siempre las DOS implementaciones concretas, para que la pagina de evidencias
/// pueda ejecutarlas una al lado de la otra y medir la diferencia de latencia.
/// </summary>
public static class FabricaCatalogo
{
    public static IServiceCollection AgregarCatalogo(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesCatalogo.Seccion).Get<OpcionesCatalogo>()
                       ?? new OpcionesCatalogo();

        servicios.AddSingleton(opciones);

        // Camino LOCAL: referencia de proyecto contra la capa de datos, sin red.
        servicios.AgregarPersistencia(configuracion);
        servicios.AddScoped<CatalogoLocal>();

        // Camino REMOTO: HttpClient tipado contra Catalogo.Api.
        servicios.AgregarCatalogoRemoto(opciones);

        // La interfaz queda enlazada al camino que indique la configuracion.
        servicios.AddScoped<ICatalogoRepuestos>(sp => opciones.Modo switch
        {
            ModoAccesoCatalogo.Remoto => sp.GetRequiredService<CatalogoRemoto>(),
            _ => sp.GetRequiredService<CatalogoLocal>()
        });

        servicios.AgregarAplicacion();

        return servicios;
    }
}
