using System.Globalization;
using System.Text.Json;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Fuentes.Mapas;

/// <summary>
/// Traduce las respuestas de Overpass y Nominatim a los DTOs de RepMatch. Esta separado del
/// HttpClient para probarlo con respuestas reales grabadas, sin red. Las reglas salen de mirar
/// respuestas reales del centro de Buenos Aires:
/// <list type="bullet">
/// <item>Un comercio puede ser un punto (node, con lat/lon) o el contorno del local (way, con
/// <c>center</c>). Se aceptan los dos.</item>
/// <item>Se descartan los que no tienen nombre: un pin sin nombre no ayuda a nadie a encontrarlo.</item>
/// <item>La direccion y el telefono los carga quien mapea y muchas veces faltan: quedan en null.</item>
/// </list>
/// </summary>
public static class LectorOpenStreetMap
{
    private const double RadioTierraMetros = 6_371_000;

    /// <summary>Los comercios de una respuesta de Overpass. Con <paramref name="centro"/> calcula
    /// la distancia y ordena por ella; sin centro (la descarga de todo el pais), por nombre.</summary>
    public static IReadOnlyList<CasaRepuestosDto> LeerCasas(string json, UbicacionDto? centro = null)
    {
        using var documento = JsonDocument.Parse(json);
        if (!documento.RootElement.TryGetProperty("elements", out var elementos) || elementos.ValueKind != JsonValueKind.Array)
            throw new JsonException("La respuesta de Overpass no trae la lista de elementos.");

        var casas = new List<CasaRepuestosDto>();
        foreach (var elemento in elementos.EnumerateArray())
        {
            if (!elemento.TryGetProperty("tags", out var etiquetas) || Texto(etiquetas, "name") is not { } nombre)
                continue;

            var punto = elemento.TryGetProperty("center", out var centroDelLocal) ? centroDelLocal : elemento;
            if (Numero(punto, "lat") is not { } latitud || Numero(punto, "lon") is not { } longitud)
                continue;

            var tipo = Texto(elemento, "type") ?? "node";
            var id = elemento.TryGetProperty("id", out var valorId) ? valorId.GetRawText() : "";

            casas.Add(new CasaRepuestosDto
            {
                IdMapa = $"{tipo}/{id}",
                Nombre = nombre,
                Latitud = latitud,
                Longitud = longitud,
                DistanciaMetros = centro is null
                    ? 0
                    : (int)Math.Round(DistanciaMetros(centro.Latitud, centro.Longitud, latitud, longitud)),
                Direccion = Direccion(etiquetas),
                Telefono = Texto(etiquetas, "phone") ?? Texto(etiquetas, "contact:phone")
            });
        }

        return [.. casas.OrderBy(c => c.DistanciaMetros).ThenBy(c => c.Nombre, StringComparer.CurrentCulture)];
    }

    /// <summary>La fecha de los datos de una respuesta de Overpass (<c>osm3s.timestamp_osm_base</c>).</summary>
    public static DateTimeOffset? LeerFecha(string json)
    {
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.TryGetProperty("osm3s", out var osm3s)
               && Texto(osm3s, "timestamp_osm_base") is { } texto
               && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fecha)
            ? fecha
            : null;
    }

    /// <summary>El primer resultado de Nominatim, o null si no encontro la zona.</summary>
    public static UbicacionDto? LeerUbicacion(string json)
    {
        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("La respuesta de Nominatim no es una lista.");

        foreach (var lugar in documento.RootElement.EnumerateArray())
        {
            // Nominatim manda lat y lon como texto.
            if (double.TryParse(Texto(lugar, "lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitud)
                && double.TryParse(Texto(lugar, "lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitud))
            {
                return new UbicacionDto
                {
                    Latitud = latitud,
                    Longitud = longitud,
                    Descripcion = Texto(lugar, "display_name")
                };
            }
        }

        return null;
    }

    /// <summary>Distancia en linea recta (Haversine). Para elegir el comercio mas cercano alcanza.</summary>
    public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radianes(double grados) => grados * Math.PI / 180;
        var dLat = Radianes(lat2 - lat1);
        var dLon = Radianes(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Radianes(lat1)) * Math.Cos(Radianes(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RadioTierraMetros * Math.Asin(Math.Sqrt(a));
    }

    private static string? Direccion(JsonElement etiquetas)
    {
        var calle = Texto(etiquetas, "addr:street");
        if (calle is null)
            return null;
        var numero = Texto(etiquetas, "addr:housenumber");
        var ciudad = Texto(etiquetas, "addr:city");
        var direccion = numero is null ? calle : $"{calle} {numero}";
        return ciudad is null ? direccion : $"{direccion}, {ciudad}";
    }

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object
        && elemento.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : null;

    private static double? Numero(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object
        && elemento.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.Number
            ? valor.GetDouble()
            : null;
}
