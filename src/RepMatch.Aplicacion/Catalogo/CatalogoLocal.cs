using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Catalogo;

/// <summary>
/// Acceso LOCAL al componente de catalogo: invocacion directa en proceso, resuelta por referencia
/// de proyecto contra el repositorio. No hay serializacion ni red de por medio.
///
/// Es una de las dos mitades del ejercicio de acceso local vs. remoto del TP Inicial; la otra es
/// RepMatch.Clientes.Rest.CatalogoRemoto. Ambas cumplen el mismo contrato y devuelven los mismos
/// DTOs, asi que la capa de presentacion no distingue cual tiene enchufada.
/// </summary>
public sealed class CatalogoLocal(
    IRepuestoRepository repuestos,
    ILogger<CatalogoLocal> log) : ICatalogoRepuestos
{
    public string Modo => "Local";

    public async Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var encontrados = await repuestos.BuscarCompatiblesAsync(
            vehiculo.AEntidad(), Mapeadores.ASistema(sistema), ct);

        log.LogInformation(
            "Catalogo[{Modo}] compatibilidad {Vehiculo} sistema={Sistema} -> {Cantidad} repuestos",
            Modo, vehiculo, sistema ?? "(todos)", encontrados.Count);

        return [.. encontrados.Select(r => r.ADto())];
    }

    public async Task<RepuestoDto?> ObtenerPorCodigoAsync(
        string codigoCanonico, CancellationToken ct = default)
    {
        var repuesto = await repuestos.ObtenerPorCodigoAsync(codigoCanonico, ct);

        log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> {Resultado}",
            Modo, codigoCanonico, repuesto is null ? "no encontrado" : "encontrado");

        return repuesto?.ADto();
    }

    public async Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default)
    {
        var todos = await repuestos.ListarAsync(ct);

        log.LogInformation("Catalogo[{Modo}] listado completo -> {Cantidad} repuestos", Modo, todos.Count);

        return [.. todos.Select(r => r.ADto())];
    }
}
