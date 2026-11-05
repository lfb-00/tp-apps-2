using System.Text.Json;
using RepMatch.IA.Texto;

namespace RepMatch.IA.Local;

/// <summary>Una frase de sintoma etiquetada a mano: sistema, urgencia, causa y tipos de repuesto.</summary>
public sealed record SintomaEtiquetado(
    string Frase, string Sistema, string Urgencia, string Causa, IReadOnlyList<string> Repuestos);

/// <summary>Un vecino del texto consultado y su similitud coseno (0 a 1).</summary>
public sealed record VecinoSintoma(SintomaEtiquetado Sintoma, double Similitud);

/// <summary>
/// Clasificador TF-IDF + k vecinos mas cercanos por similitud coseno, sobre un corpus chico de
/// sintomas en rioplatense. Es el "modelo simple" que admite la consigna y el respaldo del LLM:
/// no usa red, responde en microsegundos y es determinista, asi que tambien sirve de linea base
/// para medir al LLM en el Integrador.
///
/// Se descarto ML.NET: para ~40 frases un vector disperso a mano se explica en una pagina y no
/// suma una dependencia que haya que justificar.
/// </summary>
public sealed class ClasificadorSintomas
{
    private const string RecursoCorpus = "RepMatch.IA.sintomas.json";

    private readonly IReadOnlyList<SintomaEtiquetado> _corpus;
    private readonly Dictionary<string, double> _idf;
    private readonly List<Dictionary<string, double>> _vectores;

    public ClasificadorSintomas(IReadOnlyList<SintomaEtiquetado> corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        if (corpus.Count == 0)
            throw new ArgumentException("El corpus de sintomas no puede estar vacio.", nameof(corpus));

        _corpus = corpus;

        var documentos = corpus.Select(s => NormalizadorTexto.Raices(s.Frase)).ToList();

        // idf suavizado: un termino que aparece en todas las frases pesa poco, uno raro pesa mucho.
        _idf = documentos
            .SelectMany(d => d.Distinct())
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => Math.Log((corpus.Count + 1.0) / (g.Count() + 1.0)) + 1.0);

        _vectores = [.. documentos.Select(Vectorizar)];
    }

    public int CantidadFrases => _corpus.Count;

    /// <summary>Carga el corpus embebido en el ensamblado (Datos/sintomas.json).</summary>
    public static ClasificadorSintomas CargarCorpusIncluido()
    {
        using var flujo = typeof(ClasificadorSintomas).Assembly.GetManifestResourceStream(RecursoCorpus)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {RecursoCorpus}.");

        var corpus = JsonSerializer.Deserialize<List<SintomaEtiquetado>>(
            flujo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("El corpus de sintomas no se pudo leer.");

        return new ClasificadorSintomas(corpus);
    }

    /// <summary>Las <paramref name="k"/> frases mas parecidas al texto, con similitud mayor a cero.</summary>
    public IReadOnlyList<VecinoSintoma> Vecinos(string texto, int k)
    {
        var consulta = Vectorizar(NormalizadorTexto.Raices(texto));
        if (consulta.Count == 0)
            return [];

        return [.. _corpus
            .Select((sintoma, i) => new VecinoSintoma(sintoma, Coseno(consulta, _vectores[i])))
            .Where(v => v.Similitud > 0)
            .OrderByDescending(v => v.Similitud)
            .Take(k)];
    }

    /// <summary>Frecuencia del termino por idf, normalizado a largo 1 para que el coseno sea un
    /// producto escalar. Los terminos que no estan en el corpus se ignoran: no aportan.</summary>
    private Dictionary<string, double> Vectorizar(IReadOnlyList<string> raices)
    {
        var vector = raices
            .Where(_idf.ContainsKey)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count() * _idf[g.Key]);

        var norma = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (norma == 0)
            return [];

        foreach (var termino in vector.Keys)
            vector[termino] /= norma;

        return vector;
    }

    private static double Coseno(Dictionary<string, double> a, Dictionary<string, double> b) =>
        a.Sum(par => b.TryGetValue(par.Key, out var valor) ? par.Value * valor : 0);
}
