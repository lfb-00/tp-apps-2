using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>
/// Contrato del componente de IA de la Segunda Parte: texto libre del problema + vehiculo →
/// sistema afectado, urgencia, causas probables y repuestos candidatos.
///
/// Es el mismo esquema que <see cref="ICatalogoRepuestos"/>: una interfaz en Contracts y varias
/// estrategias intercambiables por configuracion. En proceso (RepMatch.IA, con LLM y respaldo
/// local) o por HTTP contra RepMatch.IA.Api, que es como lo pide la consigna ("expuesto como
/// servicio REST"). Quien la consume no sabe cual de las dos le toco.
/// </summary>
public interface IDiagnosticador
{
    /// <summary>"Local" (en proceso) o "Remoto" (HTTP). Se registra en el log como evidencia.</summary>
    string Modo { get; }

    /// <summary>
    /// Diagnostica el problema. Los codigos devueltos son siempre un subconjunto de
    /// <see cref="SolicitudDiagnosticoDto.Candidatos"/>: el modelo elige entre piezas reales que
    /// le entran al vehiculo, nunca inventa codigos.
    /// </summary>
    Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default);
}
