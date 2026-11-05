using RepMatch.Clientes.Rest;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.IA;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Factory del componente de IA, gemela de <see cref="FabricaCatalogo"/>: decide en el arranque
/// si <see cref="IDiagnosticador"/> se resuelve en proceso (<see cref="DiagnosticadorConRespaldo"/>,
/// la biblioteca RepMatch.IA) o por HTTP contra IA.Api (<see cref="DiagnosticadorRemoto"/>),
/// leyendo la seccion "IA" (IA__Modo). Ningun consumidor se entera de cual le toco.
/// </summary>
public static class FabricaDiagnostico
{
    public static IServiceCollection AgregarDiagnostico(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesIA.Seccion).Get<OpcionesIA>() ?? new OpcionesIA();
        servicios.AddSingleton(opciones);

        // Camino LOCAL: la biblioteca de IA en proceso (LLM si hay clave, clasificador si no).
        servicios.AgregarDiagnosticoIA(configuracion);

        // Camino REMOTO: HttpClient tipado contra IA.Api.
        servicios.AgregarDiagnosticadorRemoto(opciones);

        servicios.AddScoped<IDiagnosticador>(sp => opciones.Modo switch
        {
            ModoAccesoIA.Remoto => sp.GetRequiredService<DiagnosticadorRemoto>(),
            _ => sp.GetRequiredService<DiagnosticadorConRespaldo>()
        });

        return servicios;
    }
}
