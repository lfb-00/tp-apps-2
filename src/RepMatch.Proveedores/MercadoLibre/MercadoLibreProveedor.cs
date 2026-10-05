using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Proveedores.Configuracion;

namespace RepMatch.Proveedores.MercadoLibre;

/// <summary>
/// Adaptador REST hacia la API pública de MercadoLibre Argentina. Busca publicaciones por
/// código de repuesto dentro de la categoría "Repuestos y Accesorios" (MLA1743) y normaliza
/// la respuesta al contrato IProveedorOfertas. No requiere OAuth para búsquedas de lectura.
/// </summary>
public sealed class MercadoLibreProveedor(
    HttpClient http,
    IOptions<OpcionesProveedores> opciones,
    ILogger<MercadoLibreProveedor> log) : IProveedorOfertas
{
    private const string CategoriaRepuestos = "MLA1743";

    public string Nombre => "MercadoLibre";

    public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(
        string codigoRepuesto,
        VehiculoDto vehiculo,
        CancellationToken ct = default)
    {
        var config = opciones.Value.MercadoLibre;
        if (!config.Habilitado)
            return [];

        // Combinar código + vehículo para mejorar la relevancia de los resultados.
        var query = Uri.EscapeDataString(
            $"{codigoRepuesto} {vehiculo.Marca} {vehiculo.Modelo}".Trim());

        var url = $"sites/MLA/search?q={query}&category={CategoriaRepuestos}&limit={config.LimitePorBusqueda}";
        if (!string.IsNullOrWhiteSpace(config.AppId))
            url += $"&app_id={config.AppId}";

        try
        {
            var respuesta = await http.GetFromJsonAsync<MlBusquedaRespuesta>(url, ct);
            if (respuesta?.Results is not { Count: > 0 } items)
                return [];

            log.LogInformation(
                "MercadoLibre: {Cantidad} resultados para '{Codigo}' ({Vehiculo})",
                items.Count, codigoRepuesto, $"{vehiculo.Marca} {vehiculo.Modelo}");

            return [.. items.Select(item => new OfertaExternaDto(
                CodigoRepuesto: codigoRepuesto,
                Titulo: item.Title,
                Precio: item.Price,
                Moneda: item.CurrencyId,
                Url: item.Permalink,
                NombreTienda: item.Seller?.Nickname ?? Nombre,
                CostoEnvio: item.Shipping?.FreeShipping == true ? 0m : null,
                Disponible: item.AvailableQuantity > 0))];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            log.LogWarning(ex, "MercadoLibre: error al buscar '{Codigo}'", codigoRepuesto);
            return [];
        }
    }
}
