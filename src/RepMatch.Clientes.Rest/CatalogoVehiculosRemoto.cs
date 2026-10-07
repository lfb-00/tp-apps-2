using System.Net.Http.Json;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Clientes.Rest;

public sealed class CatalogoVehiculosRemoto(HttpClient http) : ICatalogoVehiculos
{
    public async Task<IReadOnlyList<OpcionVehiculoDto>> ListarOpcionesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<OpcionVehiculoDto>>("api/vehiculos/opciones", ct) ?? [];

    public async Task<bool> EsConfiguracionValidaAsync(VehiculoDto vehiculo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);
        var opciones = await ListarOpcionesAsync(ct);
        return opciones.Any(o =>
            o.Marca.Equals(vehiculo.Marca, StringComparison.OrdinalIgnoreCase)
            && o.Modelo.Equals(vehiculo.Modelo, StringComparison.OrdinalIgnoreCase)
            && o.AnioDesde <= vehiculo.Anio && o.AnioHasta >= vehiculo.Anio
            && (string.IsNullOrWhiteSpace(vehiculo.Motor) || o.Motor.Equals(vehiculo.Motor, StringComparison.OrdinalIgnoreCase)));
    }
}
