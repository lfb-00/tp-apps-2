using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>Piezas compatibles, las que eligio el diagnostico y el diagnostico mismo (null si la
/// IA no respondio).</summary>
public sealed record PiezasDiagnosticadas(
    IReadOnlyList<RepuestoDto> Compatibles,
    IReadOnlyList<RepuestoDto> Elegidas,
    DiagnosticoDto? Diagnostico);

/// <summary>
/// La regla que combina catalogo e IA, en un solo lugar para la busqueda guardada
/// (ObservadorCompatibilidad) y para la anonima (FachadaAplicacion):
///
/// 1. La compatibilidad con el vehiculo es el filtro duro: la resuelve el catalogo.
/// 2. La IA lee el texto libre y elige entre esas piezas. Nunca agrega una que no entre.
/// 3. Si la IA no elige ninguna, o no responde, quedan todas las compatibles: la busqueda nunca
///    sale peor que en la Primera Parte.
/// </summary>
public sealed class ServicioDiagnostico(
    ICatalogoRepuestos catalogo,
    IDiagnosticador diagnosticador,
    ILogger<ServicioDiagnostico> log)
{
    /// <summary>Las excepciones del catalogo se propagan: sin catalogo no hay busqueda, y cada
    /// llamador ya decide como contarlo. Las de la IA no: la IA es una mejora, no un requisito.</summary>
    public async Task<PiezasDiagnosticadas> DiagnosticarAsync(
        VehiculoDto vehiculo, string? texto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var compatibles = await catalogo.BuscarCompatiblesAsync(vehiculo, sistema: null, ct);
        if (compatibles.Count == 0 || string.IsNullOrWhiteSpace(texto))
            return new PiezasDiagnosticadas(compatibles, compatibles, null);

        DiagnosticoDto diagnostico;
        try
        {
            diagnostico = await diagnosticador.DiagnosticarAsync(new SolicitudDiagnosticoDto
            {
                Texto = texto.Trim(),
                Vehiculo = vehiculo,
                Candidatos = [.. compatibles.Select(r => new CandidatoDiagnosticoDto
                {
                    Codigo = r.CodigoCanonico,
                    Nombre = r.Nombre,
                    Sistema = r.Sistema
                })]
            }, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "IA[{Modo}] no respondio; se muestran todas las piezas compatibles", diagnosticador.Modo);
            return new PiezasDiagnosticadas(compatibles, compatibles, null);
        }

        var porCodigo = compatibles.ToDictionary(r => r.CodigoCanonico, StringComparer.OrdinalIgnoreCase);
        var elegidas = diagnostico.Codigos
            .Select(c => porCodigo.GetValueOrDefault(c))
            .OfType<RepuestoDto>()
            .Distinct()
            .ToList();

        log.LogInformation(
            "IA[{Modo}] fuente={Fuente} sistema={Sistema} eligio {Elegidas} de {Compatibles} piezas para {Vehiculo}",
            diagnosticador.Modo, diagnostico.Fuente, diagnostico.Sistema, elegidas.Count, compatibles.Count, vehiculo);

        return new PiezasDiagnosticadas(compatibles, elegidas.Count > 0 ? elegidas : compatibles, diagnostico);
    }
}
