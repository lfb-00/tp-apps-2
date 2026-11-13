using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;

namespace RepMatch.Aplicacion.Ofertas;

/// <summary>
/// Descarta coincidencias por nombre que contradicen la configuracion buscada. La falta de datos
/// no se interpreta como incompatibilidad: esas publicaciones siguen como probables, fuera del
/// grupo de coincidencias exactas.
/// </summary>
public static partial class ValidadorCandidatoOferta
{
    private static readonly IReadOnlyDictionary<SistemaVehiculo, string[]> TerminosPorSistema =
        new Dictionary<SistemaVehiculo, string[]>
        {
            [SistemaVehiculo.Frenos] = ["freno", "frenos", "pastilla", "pastillas", "disco", "discos", "caliper", "brake"],
            [SistemaVehiculo.Suspension] = ["suspension", "amortiguador", "amortiguadores", "espiral", "ballesta"],
            [SistemaVehiculo.Motor] = ["motor", "filtro de aceite", "filtro de aire", "bujia", "correa", "piston"],
            [SistemaVehiculo.Transmision] = ["transmision", "embrague", "caja de cambios", "clutch"],
            [SistemaVehiculo.Electrico] = ["bateria", "alternador", "burro de arranque", "fusible", "encendido"],
            [SistemaVehiculo.Refrigeracion] = ["radiador", "bomba de agua", "termostato", "refrigeracion"],
            [SistemaVehiculo.Escape] = ["escape", "silenciador", "catalizador"],
            [SistemaVehiculo.Direccion] = ["direccion", "cremallera", "terminal"],
            [SistemaVehiculo.Carroceria] = ["carroceria", "paragolpes", "guardabarro", "capot"],
            [SistemaVehiculo.Neumaticos] = ["neumatico", "cubierta", "llanta"]
        };

    [GeneratedRegex(@"(?<!\d)(?<desde>(?:19|20)\d{2})\s*(?:-|–|/|a|hasta)\s*(?<hasta>(?:19|20)\d{2})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RangoAnios();

    public static string? MotivoRechazo(
        OfertaExternaDto oferta,
        RepuestoDto pieza,
        VehiculoDto vehiculo,
        IReadOnlyList<OpcionVehiculoDto> vehiculosConocidos)
    {
        ArgumentNullException.ThrowIfNull(oferta);
        ArgumentNullException.ThrowIfNull(pieza);
        ArgumentNullException.ThrowIfNull(vehiculo);
        ArgumentNullException.ThrowIfNull(vehiculosConocidos);

        if (oferta.Coincidencia != CoincidenciasOferta.ProbablePorNombre)
            return null;

        var textoPublicacion = string.Join(' ', new[] { oferta.Titulo }.Concat(oferta.Atributos));
        var texto = Normalizar(textoPublicacion);

        if (MencionaModeloIncompatible(texto, pieza, vehiculo, vehiculosConocidos))
            return "modelo distinto";

        if (MencionaMarcaIncompatible(texto, pieza, vehiculo, vehiculosConocidos))
            return "marca distinta";

        if (AnioFueraDeRango(textoPublicacion, vehiculo.Anio))
            return "anio fuera de rango";

        if (PosicionIncompatible(texto, pieza.Nombre))
            return "posicion distinta";

        if (SistemaIncompatible(texto, pieza.Sistema))
            return "sistema distinto";

        return null;
    }

    private static bool MencionaModeloIncompatible(
        string texto,
        RepuestoDto pieza,
        VehiculoDto vehiculo,
        IReadOnlyList<OpcionVehiculoDto> opciones)
    {
        var mencionaBuscado = Contiene(texto, vehiculo.Modelo);
        var modelosAlternativos = opciones
            .Where(o => Contiene(texto, o.Modelo))
            .Where(o => !o.Modelo.Equals(vehiculo.Modelo, StringComparison.OrdinalIgnoreCase))
            .Where(o => !pieza.Aplicaciones.Any(a =>
                a.Modelo.Equals(o.Modelo, StringComparison.OrdinalIgnoreCase)
                && a.Marca.Equals(o.Marca, StringComparison.OrdinalIgnoreCase)
                && vehiculo.Anio >= a.AnioDesde && vehiculo.Anio <= a.AnioHasta))
            .Select(o => o.Modelo)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return !mencionaBuscado && modelosAlternativos.Any();
    }

    private static bool MencionaMarcaIncompatible(
        string texto,
        RepuestoDto pieza,
        VehiculoDto vehiculo,
        IReadOnlyList<OpcionVehiculoDto> opciones)
    {
        return opciones
            .Where(o => !o.Marca.Equals(vehiculo.Marca, StringComparison.OrdinalIgnoreCase))
            .Where(o => Contiene(texto, o.Marca) && Contiene(texto, o.Modelo))
            .Any(o => !pieza.Aplicaciones.Any(a =>
                a.Modelo.Equals(o.Modelo, StringComparison.OrdinalIgnoreCase)
                && a.Marca.Equals(o.Marca, StringComparison.OrdinalIgnoreCase)
                && vehiculo.Anio >= a.AnioDesde && vehiculo.Anio <= a.AnioHasta));
    }

    private static bool AnioFueraDeRango(string texto, int anio)
    {
        foreach (Match rango in RangoAnios().Matches(texto))
        {
            var desde = int.Parse(rango.Groups["desde"].Value, CultureInfo.InvariantCulture);
            var hasta = int.Parse(rango.Groups["hasta"].Value, CultureInfo.InvariantCulture);
            if (anio < Math.Min(desde, hasta) || anio > Math.Max(desde, hasta))
                return true;
        }

        return false;
    }

    private static bool PosicionIncompatible(string titulo, string nombrePieza)
    {
        var posicionOferta = Posicion(Normalizar(titulo));
        var posicionPieza = Posicion(Normalizar(nombrePieza));
        return posicionOferta is not null && posicionPieza is not null && posicionOferta != posicionPieza;
    }

    private static string? Posicion(string texto)
    {
        if (Contiene(texto, "delantero") || Contiene(texto, "delantera") || Contiene(texto, "delanteras")
            || Contiene(texto, "frontal") || Contiene(texto, "front"))
            return "delantera";
        if (Contiene(texto, "trasero") || Contiene(texto, "trasera") || Contiene(texto, "traseras")
            || Contiene(texto, "posterior") || Contiene(texto, "rear"))
            return "trasera";
        return null;
    }

    private static bool SistemaIncompatible(string texto, string sistema)
    {
        if (!Enum.TryParse<SistemaVehiculo>(sistema, true, out var esperado)
            || esperado == SistemaVehiculo.Desconocido)
            return false;

        var detectados = TerminosPorSistema
            .Where(x => x.Value.Any(t => Contiene(texto, t)))
            .Select(x => x.Key)
            .ToList();

        return detectados.Count > 0 && !detectados.Contains(esperado);
    }

    private static bool Contiene(string texto, string frase)
    {
        var termino = Normalizar(frase);
        return termino.Length > 0 && $" {texto} ".Contains($" {termino} ", StringComparison.Ordinal);
    }

    private static string Normalizar(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
                continue;
            resultado.Append(char.IsLetterOrDigit(caracter) ? char.ToLowerInvariant(caracter) : ' ');
        }

        return string.Join(' ', resultado.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
