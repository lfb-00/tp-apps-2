using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Clientes.Rest;

/// <summary>
/// Acceso REMOTO al componente de catalogo: el mismo contrato, resuelto por HTTP contra
/// RepMatch.Catalogo.Api. Aca si hay serializacion JSON, red y codigos de estado que traducir.
///
/// Es la contracara de Aplicacion.Catalogo.CatalogoLocal. Que las dos implementen
/// <see cref="ICatalogoRepuestos"/> es lo que permite intercambiarlas por configuracion sin
/// tocar una linea de la capa de presentacion.
/// </summary>
public sealed class CatalogoRemoto(HttpClient http, ILogger<CatalogoRemoto> log) : ICatalogoRepuestos
{
    public string Modo => "Remoto";

    public async Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var ruta = $"api/repuestos/compatibles?marca={Uri.EscapeDataString(vehiculo.Marca)}"
                 + $"&modelo={Uri.EscapeDataString(vehiculo.Modelo)}"
                 + $"&anio={vehiculo.Anio}";

        if (!string.IsNullOrWhiteSpace(vehiculo.Motor))
            ruta += $"&motor={Uri.EscapeDataString(vehiculo.Motor)}";

        if (!string.IsNullOrWhiteSpace(sistema))
            ruta += $"&sistema={Uri.EscapeDataString(sistema)}";

        var repuestos = await ObtenerListaAsync(ruta, ct);

        log.LogInformation(
            "Catalogo[{Modo}] compatibilidad {Vehiculo} sistema={Sistema} -> {Cantidad} repuestos",
            Modo, vehiculo, sistema ?? "(todos)", repuestos.Count);

        return repuestos;
    }

    public async Task<RepuestoDto?> ObtenerPorCodigoAsync(
        string codigoCanonico, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoCanonico);

        var respuesta = await http.GetAsync(
            $"api/repuestos/{Uri.EscapeDataString(codigoCanonico)}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> no encontrado", Modo, codigoCanonico);
            return null;
        }

        respuesta.EnsureSuccessStatusCode();

        var repuesto = await respuesta.Content.ReadFromJsonAsync<RepuestoDto>(ct);

        log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> encontrado", Modo, codigoCanonico);
        return repuesto;
    }

    public async Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default)
    {
        var repuestos = await ObtenerListaAsync("api/repuestos", ct);

        log.LogInformation("Catalogo[{Modo}] listado completo -> {Cantidad} repuestos", Modo, repuestos.Count);
        return repuestos;
    }

    private async Task<IReadOnlyList<RepuestoDto>> ObtenerListaAsync(string ruta, CancellationToken ct)
    {
        var respuesta = await http.GetAsync(ruta, ct);
        respuesta.EnsureSuccessStatusCode();

        var repuestos = await respuesta.Content.ReadFromJsonAsync<List<RepuestoDto>>(ct);
        return repuestos ?? [];
    }
}
