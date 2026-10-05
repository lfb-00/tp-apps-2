using System.Text.Json.Serialization;

namespace RepMatch.Proveedores.eBay;

/// <summary>Modelos internos que mapean la respuesta JSON de la Browse API de eBay.</summary>
internal sealed record EbayBusquedaRespuesta(
    [property: JsonPropertyName("itemSummaries")] List<EbayItem>? ItemSummaries);

internal sealed record EbayItem(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("price")] EbayPrecio Price,
    [property: JsonPropertyName("itemWebUrl")] string ItemWebUrl,
    [property: JsonPropertyName("shippingOptions")] List<EbayOpcionEnvio>? ShippingOptions,
    [property: JsonPropertyName("seller")] EbaySeller? Seller);

internal sealed record EbayPrecio(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("currency")] string Currency);

internal sealed record EbayOpcionEnvio(
    [property: JsonPropertyName("shippingCost")] EbayPrecio? ShippingCost);

internal sealed record EbaySeller(
    [property: JsonPropertyName("username")] string Username);

internal sealed record EbayTokenRespuesta(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
