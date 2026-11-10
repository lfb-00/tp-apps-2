using System.Text.RegularExpressions;

namespace RepMatch.IA.Texto;

/// <summary>
/// Saca mails y telefonos del texto antes de que salga hacia un proveedor externo. Para
/// diagnosticar un ruido al frenar no hace falta saber como contactar a la persona, y el texto
/// libre es justo donde la gente los escribe ("llamame al 11 5555-1234"). Es parte del
/// requerimiento de privacidad de la IA.
/// </summary>
public static partial class FiltroDatosPersonales
{
    public static string Filtrar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var sinMails = Email().Replace(texto, "[email]");
        return Telefono().Replace(sinMails, m =>
            m.Value.Count(char.IsDigit) >= DigitosMinimosTelefono ? "[telefono]" : m.Value);
    }

    /// <summary>Un telefono argentino tiene al menos 10 digitos (11 1234-5678). Con menos se
    /// confundiria con un anio o un kilometraje ("2015", "120000 km"), que si sirven.</summary>
    private const int DigitosMinimosTelefono = 10;

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+?\d[\d\s\-()]{6,}\d")]
    private static partial Regex Telefono();
}
