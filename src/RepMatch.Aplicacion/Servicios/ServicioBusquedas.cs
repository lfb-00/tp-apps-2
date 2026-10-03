using FluentValidation;
using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Common.Resultados;
using RepMatch.Common.Validacion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Crea y consulta búsquedas. No resuelve piezas: eso lo hace un observador de
/// <c>BusquedaCreada</c> después de que la búsqueda queda persistida.
/// </summary>
public sealed class ServicioBusquedas(
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
    IUnitOfWork unidadDeTrabajo,
    IDespachadorEventos despachador,
    IValidator<CrearBusquedaDto> validador,
    ILogger<ServicioBusquedas> log)
{
    public async Task<IReadOnlyList<BusquedaDto>> ListarRecientesAsync(
        int cantidad = 20, CancellationToken ct = default)
    {
        var recientes = await busquedas.ListarRecientesAsync(cantidad, ct);
        return [.. recientes.Select(b => b.ADto())];
    }

    public async Task<IReadOnlyList<BusquedaDto>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken ct = default)
    {
        var delCliente = await busquedas.ListarPorClienteAsync(clienteId, ct);
        return [.. delCliente.Select(b => b.ADto())];
    }

    public async Task<BusquedaDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(id, ct);
        return busqueda?.ADto();
    }

    public async Task<ResultadoOperacion<BusquedaDto>> CrearAsync(
        CrearBusquedaDto dto, CancellationToken ct = default)
    {
        var validacion = await validador.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<BusquedaDto>.Falla(validacion.Errores);

        if (await clientes.ObtenerPorIdAsync(dto.ClienteId, ct) is null)
            return ResultadoOperacion<BusquedaDto>.Falla($"No existe el cliente {dto.ClienteId}.");

        try
        {
            var busqueda = new Busqueda(dto.ClienteId, dto.Vehiculo.AEntidad(), dto.TextoLibre);
            var pendientes = busqueda.EventosDominio.ToArray();

            await busquedas.AgregarAsync(busqueda, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);
            busqueda.LimpiarEventos();

            await despachador.DespacharAsync(pendientes, ct);

            var actualizada = await busquedas.ObtenerPorIdAsync(busqueda.Id, ct)
                ?? throw new InvalidOperationException($"La búsqueda {busqueda.Id} no quedó persistida.");

            log.LogInformation(
                "Búsqueda {BusquedaId} registrada. Estado {Estado}",
                actualizada.Id, actualizada.Estado);

            return ResultadoOperacion<BusquedaDto>.Exito(actualizada.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<BusquedaDto>.Falla(ex.Message);
        }
    }
}
