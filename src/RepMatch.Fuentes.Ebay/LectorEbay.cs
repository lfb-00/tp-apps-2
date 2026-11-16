using System.Globalization;
using System.Text;
using System.Text.Json;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Fuentes.Ebay;

/// <summary>
/// Traduce la respuesta de item_summary/search de la Browse API a <see cref="OfertaExternaDto"/>.
///
/// En eBay se busca por numero de fabricante, y solo se acepta una publicacion cuyo titulo nombra
/// ese numero: asi toda oferta de eBay es una coincidencia exacta por codigo. Una busqueda por
/// "W 712/83" que trae otro filtro (eBay completa la busqueda con resultados parecidos) se descarta.
///
/// No se guarda nada que identifique a la persona que vende (usuario, puntaje): la app declara ante
/// eBay que no persiste datos de sus usuarios (Marketplace Account Deletion), y eso tiene que ser
/// cierto. De cada publicacion quedan titulo, precio, enlace, estado e identificador.
/// </summary>
public static class LectorEbay
{
    public static IReadOnlyList<OfertaExternaDto> Leer(
        string json, ConsultaOfertasDto consulta, string codigo, DateTimeOffset capturadaEn)
    {
        using var documento = JsonDocument.Parse(json);
        if (!documento.RootElement.TryGetProperty("itemSummaries", out var items)
            || items.ValueKind != JsonValueKind.Array)
            return [];

        var buscado = SoloAlfanumerico(CodigosRepuesto.SinMarca(codigo));
        if (buscado.Length == 0)
            return [];

        var ofertas = new List<OfertaExternaDto>();
        foreach (var item in items.EnumerateArray())
        {
            var titulo = Texto(item, "title");
            var url = Texto(item, "itemWebUrl");
            if (titulo is null || url is null || !TryPrecio(item, "price", out var precio, out var moneda))
                continue;

            if (!SoloAlfanumerico(titulo).Contains(buscado, StringComparison.Ordinal))
                continue;

            decimal? lista = null;
            if (item.TryGetProperty("marketingPrice", out var marketing)
                && TryPrecio(marketing, "originalPrice", out var original, out var monedaOriginal)
                && monedaOriginal == moneda && original > precio)
                lista = original;

            ofertas.Add(new OfertaExternaDto
            {
                CodigoRepuesto = consulta.CodigoRepuesto,
                Tienda = OpcionesEbay.NombreTienda,
                Titulo = titulo,
                Precio = precio,
                PrecioLista = lista,
                Moneda = moneda,
                Url = url,
                Coincidencia = CoincidenciasOferta.ExactoPorCodigo,
                Atributos = Texto(item, "condition") is { } estado ? [estado] : [],
                CapturadaEn = capturadaEn,
                Referencia = Texto(item, "itemId")
            });
        }

        return ofertas;
    }


    private static bool TryPrecio(JsonElement elemento, string propiedad, out decimal monto, out string moneda)
    {
        monto = 0;
        moneda = string.Empty;
        if (!elemento.TryGetProperty(propiedad, out var precio)
            || Texto(precio, "currency") is not { Length: 3 } codigoMoneda
            || !decimal.TryParse(Texto(precio, "value"), NumberStyles.Number, CultureInfo.InvariantCulture, out monto)
            || monto <= 0)
            return false;

        moneda = codigoMoneda.ToUpperInvariant();
        return true;
    }

    private static string SoloAlfanumerico(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(c))
                resultado.Append(char.ToUpperInvariant(c));
        return resultado.ToString();
    }

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object
        && elemento.TryGetProperty(propiedad, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : null;
}
