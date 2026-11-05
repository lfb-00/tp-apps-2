using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepMatch.IA.Llm;
using RepMatch.IA.Local;

namespace RepMatch.IA;

public static class ExtensionesIA
{
    /// <summary>
    /// Registra el componente de IA en proceso: el clasificador local, el adapter del LLM y
    /// <see cref="DiagnosticadorConRespaldo"/>, que los combina. Como con el catalogo, registra
    /// el tipo CONCRETO y no la interfaz: IA.Api la enlaza siempre a esta implementacion y la Web
    /// elige entre esta y la remota segun IA:Modo.
    /// </summary>
    public static IServiceCollection AgregarDiagnosticoIA(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(OpcionesLlm.Seccion).Get<OpcionesLlm>() ?? new OpcionesLlm();
        servicios.AddSingleton(opciones);

        // El corpus se vectoriza una sola vez al arrancar.
        servicios.AddSingleton(_ => ClasificadorSintomas.CargarCorpusIncluido());
        servicios.AddSingleton<DiagnosticadorLocal>();

        servicios.AddHttpClient<DiagnosticadorLlm>(http =>
        {
            http.BaseAddress = new Uri(opciones.UrlBase.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos);
            http.DefaultRequestHeaders.Add("User-Agent", "RepMatch.IA");
        });

        servicios.AddScoped(sp => new DiagnosticadorConRespaldo(
            sp.GetRequiredService<DiagnosticadorLlm>(),
            sp.GetRequiredService<DiagnosticadorLocal>(),
            opciones,
            sp.GetRequiredService<ILogger<DiagnosticadorConRespaldo>>()));

        return servicios;
    }
}
