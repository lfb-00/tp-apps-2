using System.Text.Json.Serialization;

namespace RepMatch.Proveedores.MercadoLibre;

/// <summary>Modelos internos que mapean la respuesta JSON de la API de MercadoLibre.</summary>
internal sealed record MlBusquedaRespuesta(
    [property: JsonPropertyName("results")] List<MlItem> Results);

internal sealed record MlItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("currency_id")] string CurrencyId,
    [property: JsonPropertyName("permalink")] string Permalink,
    [property: JsonPropertyName("shipping")] MlShipping? Shipping,
    [property: JsonPropertyName("seller")] MlSeller? Seller,
    [property: JsonPropertyName("available_quantity")] int AvailableQuantity);

internal sealed record MlShipping(
    [property: JsonPropertyName("free_shipping")] bool FreeShipping);

internal sealed record MlSeller(
    [property: JsonPropertyName("nickname")] string Nickname);
