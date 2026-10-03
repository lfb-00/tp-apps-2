using RepMatch.Aplicacion.Servicios;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion.Fachada;

/// <summary>
/// Única puerta de la presentación hacia clientes, búsquedas y catálogo. Esconde los servicios,
/// el contrato del catálogo y el despacho de eventos.
/// </summary>
public sealed class FachadaAplicacion(
    ServicioClientes clientes,
    ServicioBusquedas busquedas,
    ICatalogoRepuestos catalogo)
{
    public Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct = default) =>
        clientes.ListarAsync(ct);

    public Task<ClienteDto?> ObtenerClienteAsync(Guid id, CancellationToken ct = default) =>
        clientes.ObtenerAsync(id, ct);

    public Task<ClienteDto?> ObtenerClientePorEmailAsync(string email, CancellationToken ct = default) =>
        clientes.ObtenerPorEmailAsync(email, ct);

    public Task<ResultadoOperacion<ClienteDto>> RegistrarClienteAsync(
        CrearClienteDto dto, CancellationToken ct = default) =>
        clientes.RegistrarAsync(dto, ct);

    public Task<ResultadoOperacion<ClienteDto>> AgregarVehiculoAsync(
        Guid clienteId, VehiculoDto vehiculo, string? vin = null, string? alias = null,
        CancellationToken ct = default) =>
        clientes.AgregarVehiculoAsync(clienteId, vehiculo, vin, alias, ct);

    public Task<IReadOnlyList<BusquedaDto>> ListarBusquedasRecientesAsync(
        int cantidad = 20, CancellationToken ct = default) =>
        busquedas.ListarRecientesAsync(cantidad, ct);

    public Task<IReadOnlyList<BusquedaDto>> ListarBusquedasDelClienteAsync(
        Guid clienteId, CancellationToken ct = default) =>
        busquedas.ListarPorClienteAsync(clienteId, ct);

    public Task<ResultadoOperacion<BusquedaDto>> RegistrarBusquedaAsync(
        CrearBusquedaDto dto, CancellationToken ct = default) =>
        busquedas.CrearAsync(dto, ct);

    /// <summary>
    /// Guarda la búsqueda y devuelve las piezas del catálogo para los códigos que quedaron
    /// asignados. La presentación no resuelve códigos por su cuenta.
    /// </summary>
    public async Task<ResultadoOperacion<ResultadoBusquedaDto>> BuscarAsync(
        CrearBusquedaDto dto, CancellationToken ct = default)
    {
        var creada = await busquedas.CrearAsync(dto, ct);
        if (creada.EsFallido)
            return ResultadoOperacion<ResultadoBusquedaDto>.Falla(creada.Errores);

        if (creada.Valor.CodigosObjetivo.Count == 0)
            return ResultadoOperacion<ResultadoBusquedaDto>.Exito(new ResultadoBusquedaDto
            {
                Busqueda = creada.Valor
            });

        var compatibles = await ConsultarAsync(
            token => catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, token), ct);
        if (compatibles.EsFallido)
            return ResultadoOperacion<ResultadoBusquedaDto>.Falla(compatibles.Errores);

        var porCodigo = compatibles.Valor.ToDictionary(
            r => r.CodigoCanonico, StringComparer.OrdinalIgnoreCase);

        var piezas = creada.Valor.CodigosObjetivo
            .Select(codigo => porCodigo.GetValueOrDefault(codigo))
            .OfType<RepuestoDto>()
            .ToList();

        return ResultadoOperacion<ResultadoBusquedaDto>.Exito(new ResultadoBusquedaDto
        {
            Busqueda = creada.Valor,
            Piezas = piezas
        });
    }

    public Task<ResultadoOperacion<IReadOnlyList<RepuestoDto>>> BuscarRepuestosAsync(
        VehiculoDto vehiculo, string? sistema, CancellationToken ct = default) =>
        ConsultarAsync(token => catalogo.BuscarCompatiblesAsync(vehiculo, sistema, token), ct);

    public Task<ResultadoOperacion<IReadOnlyList<RepuestoDto>>> ListarRepuestosAsync(
        CancellationToken ct = default) =>
        ConsultarAsync(catalogo.ListarAsync, ct);

    private static async Task<ResultadoOperacion<IReadOnlyList<RepuestoDto>>> ConsultarAsync(
        Func<CancellationToken, Task<IReadOnlyList<RepuestoDto>>> consulta, CancellationToken ct)
    {
        try
        {
            return ResultadoOperacion<IReadOnlyList<RepuestoDto>>.Exito(await consulta(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ResultadoOperacion<IReadOnlyList<RepuestoDto>>.Falla(
                "No se pudo consultar el catálogo. Probá de nuevo en unos segundos.");
        }
    }
}
