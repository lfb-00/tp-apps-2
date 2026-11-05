using System.Globalization;
using System.Text;

namespace RepMatch.IA.Texto;

/// <summary>
/// Normalizacion del texto libre antes de compararlo: minusculas, sin tildes, sin signos y sin
/// palabras vacias. Sin esto "Frenó", "frenos" y "FRENAR" serian tres palabras distintas.
/// </summary>
public static class NormalizadorTexto
{
    /// <summary>Largo del prefijo que se usa como raiz. Es un stemming rudimentario pero alcanza
    /// para el rioplatense de un formulario: "frenar", "freno", "frenando" → "fren".</summary>
    private const int LargoRaiz = 4;

    private static readonly HashSet<string> PalabrasVacias =
    [
        "a", "al", "algo", "como", "con", "cuando", "de", "del", "el", "en", "es", "esta", "hace",
        "la", "las", "le", "lo", "los", "me", "mi", "mucho", "muy", "o", "para", "pero", "por",
        "que", "se", "su", "tengo", "un", "una", "uno", "y", "ya", "auto", "coche"
    ];

    /// <summary>Palabras del texto, normalizadas y sin palabras vacias.</summary>
    public static IReadOnlyList<string> Palabras(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return [];

        var limpio = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            limpio.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return [.. limpio.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !PalabrasVacias.Contains(p))];
    }

    /// <summary>Raices de las palabras del texto: es lo que compara el clasificador.</summary>
    public static IReadOnlyList<string> Raices(string texto) =>
        [.. Palabras(texto).Select(p => p.Length > LargoRaiz ? p[..LargoRaiz] : p)];
}
