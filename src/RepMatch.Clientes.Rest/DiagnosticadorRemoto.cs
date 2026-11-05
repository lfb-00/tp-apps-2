using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Clientes.Rest;

/// <summary>
/// Acceso REMOTO al componente de IA: el mismo <see cref="IDiagnosticador"/>, resuelto por HTTP
/// contra RepMatch.IA.Api. Es el Adapter equivalente a <see cref="CatalogoRemoto"/>: traduce la
/// llamada a JSON y los errores de red a excepciones que el llamador ya sabe tratar.
/// </summary>
public sealed class DiagnosticadorRemoto(HttpClient http, ILogger<DiagnosticadorRemoto> log) : IDiagnosticador
{
    public string Modo => "Remoto";

    public async Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var respuesta = await http.PostAsJsonAsync("api/diagnostico", solicitud, ct);
        respuesta.EnsureSuccessStatusCode();

        var diagnostico = await respuesta.Content.ReadFromJsonAsync<DiagnosticoDto>(ct)
            ?? throw new HttpRequestException("IA.Api respondio sin cuerpo.");

        log.LogInformation(
            "IA[{Modo}] fuente={Fuente} sistema={Sistema} codigos={Codigos} latencia={Milisegundos} ms",
            Modo, diagnostico.Fuente, diagnostico.Sistema, diagnostico.Codigos.Count, diagnostico.LatenciaMs);

        return diagnostico;
    }
}
