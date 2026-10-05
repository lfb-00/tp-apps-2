using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Proveedores.Configuracion;

namespace RepMatch.Proveedores.eBay;

/// <summary>
/// Adaptador REST hacia la Browse API de eBay. Busca repuestos en la categoría "Auto Parts and
/// Accessories" (33559) usando OAuth2 client credentials. El token se cachea en memoria hasta
/// 5 minutos antes de su vencimiento para no pedir uno nuevo en cada búsqueda.
/// </summary>
public sealed class EbayProveedor(
    HttpClient http,
    IMemoryCache cache,
    IOptions<OpcionesProveedores> opciones,
    ILogger<EbayProveedor> log) : IProveedorOfertas
{
    private const string CategoriaRepuestos = "33559";
    private const string ClaveToken = "ebay_oauth_token";

    public string Nombre => "eBay";

    public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(
        string codigoRepuesto,
        VehiculoDto vehiculo,
        CancellationToken ct = default)
    {
        var config = opciones.Value.eBay;
        if (!config.Habilitado)
            return [];

        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            log.LogWarning("eBay: ClientId o ClientSecret no configurados.");
            return [];
        }

        try
        {
            var token = await ObtenerTokenAsync(config, ct);
            if (string.IsNullOrWhiteSpace(token))
                return [];

            var query = Uri.EscapeDataString(
                $"{codigoRepuesto} {vehiculo.Marca} {vehiculo.Modelo}".Trim());

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"buy/browse/v1/item_summary/search?q={query}&category_ids={CategoriaRepuestos}&limit={config.LimitePorBusqueda}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var respuesta = await response.Content.ReadFromJsonAsync<EbayBusquedaRespuesta>(ct);
            if (respuesta?.ItemSummaries is not { Count: > 0 } items)
                return [];

            log.LogInformation(
                "eBay: {Cantidad} resultados para '{Codigo}' ({Vehiculo})",
                items.Count, codigoRepuesto, $"{vehiculo.Marca} {vehiculo.Modelo}");

            return [.. items.Select(item =>
            {
                var precio = decimal.TryParse(item.Price.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0m;

                decimal? envio = null;
                var opcionEnvio = item.ShippingOptions?.FirstOrDefault()?.ShippingCost;
                if (opcionEnvio is not null
                    && decimal.TryParse(opcionEnvio.Value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var e))
                    envio = e;

                return new OfertaExternaDto(
                    CodigoRepuesto: codigoRepuesto,
                    Titulo: item.Title,
                    Precio: precio,
                    Moneda: item.Price.Currency,
                    Url: item.ItemWebUrl,
                    NombreTienda: item.Seller?.Username ?? Nombre,
                    CostoEnvio: envio);
            })];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            log.LogWarning(ex, "eBay: error al buscar '{Codigo}'", codigoRepuesto);
            return [];
        }
    }

    private async Task<string?> ObtenerTokenAsync(OpcionesEbay config, CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveToken, out string? tokenCacheado) && tokenCacheado is not null)
            return tokenCacheado;

        var credenciales = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://api.ebay.com/identity/v1/oauth2/token");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciales);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "https://api.ebay.com/oauth/api_scope"
        });

        var respuesta = await http.SendAsync(tokenRequest, ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogWarning("eBay: no se pudo obtener el token OAuth ({Status})", respuesta.StatusCode);
            return null;
        }

        var tokenDto = await respuesta.Content.ReadFromJsonAsync<EbayTokenRespuesta>(ct);
        if (tokenDto is null)
            return null;

        // Guardar el token 5 minutos antes de que venza para evitar requests con token expirado.
        var ttl = TimeSpan.FromSeconds(tokenDto.ExpiresIn - 300);
        cache.Set(ClaveToken, tokenDto.AccessToken, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromMinutes(5));

        log.LogInformation("eBay: token OAuth obtenido, expira en {Segundos}s", tokenDto.ExpiresIn);
        return tokenDto.AccessToken;
    }
}
