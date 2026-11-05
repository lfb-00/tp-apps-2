using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.IA.Llm;
using RepMatch.IA.Local;

namespace RepMatch.IA;

/// <summary>
/// La estrategia que se registra como <see cref="IDiagnosticador"/> en proceso: intenta el LLM y,
/// si no hay clave, falla la red, se vence el timeout o la respuesta no respeta el esquema,
/// conmuta al clasificador local y lo deja escrito en el log. Para quien la llama siempre hay
/// diagnostico; la diferencia queda en <see cref="DiagnosticoDto.Fuente"/>.
///
/// Recibe las dos estrategias como <see cref="IDiagnosticador"/> y no como tipos concretos: asi
/// las pruebas pueden simular un LLM que falla sin levantar un servidor.
/// </summary>
public sealed class DiagnosticadorConRespaldo(
    IDiagnosticador llm,
    IDiagnosticador respaldo,
    OpcionesLlm opciones,
    ILogger<DiagnosticadorConRespaldo> log) : IDiagnosticador
{
    public string Modo => "Local";

    public async Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var reloj = Stopwatch.StartNew();
        DiagnosticoDto diagnostico;

        if (!opciones.EstaConfigurado)
        {
            diagnostico = await respaldo.DiagnosticarAsync(solicitud, ct);
        }
        else
        {
            try
            {
                diagnostico = await llm.DiagnosticarAsync(solicitud, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is
                HttpRequestException or TaskCanceledException or JsonException or RespuestaLlmInvalidaException)
            {
                log.LogWarning(
                    "LLM no disponible ({Motivo}: {Mensaje}); conmutando al clasificador local",
                    ex.GetType().Name, ex.Message);
                diagnostico = await respaldo.DiagnosticarAsync(solicitud, ct);
            }
        }

        // La latencia que importa es la que sufre quien busca, incluido el intento fallido al LLM.
        diagnostico = diagnostico with { LatenciaMs = reloj.ElapsedMilliseconds };

        // Se registra el largo del texto y no el texto: puede traer datos de la persona.
        log.LogInformation(
            "Diagnostico fuente={Fuente} sistema={Sistema} urgencia={Urgencia} codigos={Codigos} " +
            "candidatos={Candidatos} texto={Caracteres} caracteres latencia={Milisegundos} ms",
            diagnostico.Fuente, diagnostico.Sistema, diagnostico.Urgencia, diagnostico.Codigos.Count,
            solicitud.Candidatos.Count, solicitud.Texto.Length, diagnostico.LatenciaMs);

        return diagnostico;
    }
}
