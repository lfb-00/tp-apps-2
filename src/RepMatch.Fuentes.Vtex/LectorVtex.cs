using System.Globalization;
using System.Text;
using System.Text.Json;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Fuentes.Vtex;

/// <summary>
/// Traduce la respuesta del buscador de catalogo de VTEX
/// (<c>/api/catalog_system/pub/products/search</c>) a <see cref="OfertaExternaDto"/>. Esta
/// separado del HttpClient para poder probarlo con respuestas reales grabadas, sin red.
///
/// Las reglas salen de mirar respuestas reales de easy.com.ar y fravega.com:
/// <list type="bullet">
/// <item>Se descarta lo que esta fuera de las categorias configuradas: buscar "bateria" trae
/// taladros, pilas y baterias de herramientas.</item>
/// <item>Por nombre, el titulo tiene que empezar con la misma palabra que la pieza: asi "Bateria
/// para Auto 12V" entra y "Cargador de Bateria" no, aunque los dos esten en Automotor.</item>
/// <item>Por codigo, el codigo tiene que aparecer en la publicacion: el buscador de VTEX
/// tolera errores de tipeo y devolveria cualquier cosa parecida.</item>
/// <item>Fravega es un marketplace: cada producto trae varios vendedores y el que la tienda marca
/// por defecto puede estar sin stock. Se toma el vendedor disponible mas barato.</item>
/// </list>
/// </summary>
public static class LectorVtex
{
    public static IReadOnlyList<OfertaExternaDto> Leer(
        string json, OpcionesTiendaVtex tienda, ConsultaOfertasDto consulta, string termino, bool esCodigo)
    {
        ArgumentNullException.ThrowIfNull(tienda);
        ArgumentNullException.ThrowIfNull(consulta);

        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("La respuesta de VTEX no es una lista de productos.");

        var ofertas = new List<OfertaExternaDto>();
        foreach (var producto in documento.RootElement.EnumerateArray())
        {
            var titulo = Texto(producto, "productName");
            var url = Texto(producto, "link");
            if (titulo is null || url is null || !Uri.TryCreate(url, UriKind.Absolute, out _))
                continue;

            if (!EstaEnCategoria(producto, tienda.PrefijosCategoria))
                continue;

            var mejor = MejorOferta(producto);
            if (mejor is null)
                continue;

            if (esCodigo ? !MencionaCodigo(producto, mejor.Value.Item, termino) : !EmpiezaComo(titulo, consulta.Nombre))
                continue;

            var (item, precio, precioLista, vendedor) = mejor.Value;
            ofertas.Add(new OfertaExternaDto
            {
                CodigoRepuesto = consulta.CodigoRepuesto,
                Tienda = tienda.Nombre,
                Titulo = titulo.Trim(),
                Precio = precio,
                PrecioLista = precioLista > precio ? precioLista : null,
                Moneda = tienda.Moneda,
                Url = url,
                Coincidencia = esCodigo ? CoincidenciasOferta.PorCodigo : CoincidenciasOferta.PorNombre,
                Marca = Vacio(Texto(producto, "brand")),
                Ean = Vacio(Texto(item, "ean")),
                Referencia = Vacio(Texto(producto, "productId")) ?? Vacio(Texto(item, "itemId")),
                Vendedor = Vacio(vendedor)
            });
        }

        return ofertas;
    }

    private static bool EstaEnCategoria(JsonElement producto, IReadOnlyList<string> prefijos)
    {
        if (prefijos.Count == 0)
            return true;

        if (!producto.TryGetProperty("categories", out var categorias) || categorias.ValueKind != JsonValueKind.Array)
            return false;

        return categorias.EnumerateArray()
            .Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() : null)
            .OfType<string>()
            .Any(c => prefijos.Any(p => c.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>El vendedor con stock y precio mas bajo, entre todos los items (variantes) del producto.</summary>
    private static (JsonElement Item, decimal Precio, decimal PrecioLista, string? Vendedor)? MejorOferta(JsonElement producto)
    {
        if (!producto.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        (JsonElement, decimal, decimal, string?)? mejor = null;
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("sellers", out var vendedores) || vendedores.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var vendedor in vendedores.EnumerateArray())
            {
                if (!vendedor.TryGetProperty("commertialOffer", out var oferta))
                    continue;

                var precio = Numero(oferta, "Price");
                var disponible = oferta.TryGetProperty("IsAvailable", out var d) && d.ValueKind == JsonValueKind.True;
                if (!disponible || precio <= 0 || Numero(oferta, "AvailableQuantity") <= 0)
                    continue;

                if (mejor is null || precio < mejor.Value.Item2)
                    mejor = (item, precio, Numero(oferta, "ListPrice"), Texto(vendedor, "sellerName"));
            }
        }

        return mejor;
    }

    private static bool MencionaCodigo(JsonElement producto, JsonElement item, string codigo)
    {
        var buscado = SoloAlfanumerico(codigo);
        if (buscado.Length == 0)
            return false;

        var lugares = new List<string?>
        {
            Texto(producto, "productName"),
            Texto(producto, "productReference"),
            Texto(item, "nameComplete"),
            Texto(item, "ean")
        };
        if (item.TryGetProperty("referenceId", out var referencias) && referencias.ValueKind == JsonValueKind.Array)
            lugares.AddRange(referencias.EnumerateArray().Select(r => Texto(r, "Value")));

        return lugares.OfType<string>().Any(l => SoloAlfanumerico(l).Contains(buscado, StringComparison.Ordinal));
    }

    private static bool EmpiezaComo(string titulo, string nombrePieza)
    {
        var primera = Palabras(nombrePieza).FirstOrDefault();
        return primera is not null && Palabras(titulo).FirstOrDefault() == primera;
    }

    /// <summary>Minusculas y sin tildes: "Batería" y "Bateria" son la misma palabra.</summary>
    private static IEnumerable<string> Palabras(string texto)
    {
        var limpio = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            limpio.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string SoloAlfanumerico(string texto) =>
        string.Concat(Palabras(texto));

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static decimal Numero(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.Number
            ? valor.GetDecimal()
            : 0;

    private static string? Vacio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
