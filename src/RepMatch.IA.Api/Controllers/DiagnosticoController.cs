using Microsoft.AspNetCore.Mvc;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.IA.Api.Controllers;

/// <summary>
/// El componente de IA expuesto como servicio REST, como pide la consigna de la Segunda Parte.
/// Aca <see cref="IDiagnosticador"/> se resuelve siempre en proceso (LLM con respaldo local):
/// este servicio ES el que diagnostica.
/// </summary>
[ApiController]
[Route("api/diagnostico")]
[Produces("application/json")]
public sealed class DiagnosticoController(IDiagnosticador diagnosticador) : ControllerBase
{
    /// <summary>
    /// Diagnostica un problema descripto en texto libre. Devuelve sistema, urgencia, causas
    /// probables y los codigos elegidos entre los candidatos enviados; nunca codigos que no
    /// esten en esa lista. Siempre responde: si el LLM falla, contesta el clasificador local y
    /// la respuesta lo dice en "fuente".
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DiagnosticoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiagnosticoDto>> Diagnosticar(
        [FromBody] SolicitudDiagnosticoDto solicitud, CancellationToken ct) =>
        Ok(await diagnosticador.DiagnosticarAsync(solicitud, ct));
}
