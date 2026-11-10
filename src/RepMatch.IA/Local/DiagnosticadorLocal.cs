using System.Diagnostics;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.IA.Texto;

namespace RepMatch.IA.Local;

/// <summary>
/// Diagnostico sin red con <see cref="ClasificadorSintomas"/>. Es la red de seguridad de la
/// defensa: si el LLM no tiene clave, se queda sin cupo o tarda, el componente obligatorio de IA
/// sigue respondiendo. Marca la respuesta como "fallback-local" para que sea auditable.
/// </summary>
public sealed class DiagnosticadorLocal(ClasificadorSintomas clasificador) : IDiagnosticador
{
    public const string NombreFuente = "fallback-local";

    /// <summary>Vecinos que votan. Con un corpus chico, mas de 4 mete ruido de otros sistemas.</summary>
    private const int K = 4;

    /// <summary>Por debajo de esta similitud el texto no se parece a ningun sintoma conocido.</summary>
    private const double SimilitudMinima = 0.15;

    public string Modo => "Local";

    public Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var reloj = Stopwatch.StartNew();
        var vecinos = clasificador.Vecinos(solicitud.Texto, K);

        if (vecinos.Count == 0 || vecinos[0].Similitud < SimilitudMinima)
            return Task.FromResult(SinDiagnostico(reloj));

        // Vota cada vecino con su similitud; gana el sistema con mas peso acumulado.
        var total = vecinos.Sum(v => v.Similitud);
        var ganador = vecinos
            .GroupBy(v => v.Sintoma.Sistema)
            .OrderByDescending(g => g.Sum(v => v.Similitud))
            .First();

        var delSistema = ganador.OrderByDescending(v => v.Similitud).ToList();

        var causas = delSistema
            .GroupBy(v => v.Sintoma.Causa)
            .Select(g => new CausaProbableDto
            {
                Causa = g.Key,
                Confianza = Math.Round(g.Sum(v => v.Similitud) / total, 2)
            })
            .OrderByDescending(c => c.Confianza)
            .Take(3)
            .ToList();

        var tipos = delSistema.SelectMany(v => v.Sintoma.Repuestos).Distinct().ToList();

        return Task.FromResult(new DiagnosticoDto
        {
            Sistema = ganador.Key,
            Urgencia = delSistema[0].Sintoma.Urgencia,
            Causas = causas,
            Codigos = ElegirCandidatos(solicitud.Candidatos, tipos, ganador.Key),
            Fuente = NombreFuente,
            LatenciaMs = reloj.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// Traduce los tipos de repuesto del corpus ("filtro aire") a codigos reales: un candidato
    /// entra si su nombre contiene todas las palabras del tipo. Si ningun tipo coincide, se queda
    /// con los candidatos del sistema diagnosticado, que siguen siendo mejores que todos.
    /// </summary>
    public static IReadOnlyList<string> ElegirCandidatos(
        IReadOnlyList<CandidatoDiagnosticoDto> candidatos, IReadOnlyList<string> tipos, string sistema)
    {
        var porTipo = tipos
            .SelectMany(tipo =>
            {
                var palabras = NormalizadorTexto.Palabras(tipo);
                return candidatos.Where(c =>
                {
                    var nombre = NormalizadorTexto.Palabras(c.Nombre);
                    return palabras.All(nombre.Contains);
                });
            })
            .Select(c => c.Codigo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (porTipo.Count > 0)
            return porTipo;

        return [.. candidatos
            .Where(c => string.Equals(c.Sistema, sistema, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Codigo)];
    }

    private static DiagnosticoDto SinDiagnostico(Stopwatch reloj) => new()
    {
        Sistema = "Desconocido",
        Urgencia = "Desconocida",
        Fuente = NombreFuente,
        LatenciaMs = reloj.ElapsedMilliseconds
    };
}
