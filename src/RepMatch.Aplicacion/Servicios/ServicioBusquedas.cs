using FluentValidation;
using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Common.Resultados;
using RepMatch.Common.Validacion;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Logica de negocio de las busquedas (el "Pedido" de la consigna).
///
/// En el TP Inicial una busqueda se crea y se resuelve contra el catalogo propio por
/// compatibilidad. En la Segunda Parte, el paso de decidir que codigos buscar pasa al componente
/// de IA y la consulta a las tiendas externas se dispara por cola; el contrato publico de este
/// servicio no cambia.
/// </summary>
public sealed class ServicioBusquedas(
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
    ICatalogoRepuestos catalogo,
    IUnitOfWork unidadDeTrabajo,
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

    /// <summary>
    /// Registra la consulta del cliente y le asigna los codigos de repuesto candidatos segun la
    /// compatibilidad con su vehiculo. Consulta el catalogo a traves de
    /// <see cref="ICatalogoRepuestos"/>, asi que funciona igual con el catalogo local o el remoto.
    /// </summary>
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

            var compatibles = await catalogo.BuscarCompatiblesAsync(dto.Vehiculo, sistema: null, ct);

            if (compatibles.Count > 0)
            {
                busqueda.AsignarCodigosObjetivo(compatibles.Select(r => r.CodigoCanonico));
            }
            else
            {
                busqueda.Fallar(
                    $"Todavia no tenemos repuestos catalogados para un {dto.Vehiculo}.");
            }

            await busquedas.AgregarAsync(busqueda, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogInformation(
                "Busqueda {BusquedaId} creada via catalogo[{Modo}]: {Codigos} codigos objetivo, estado {Estado}",
                busqueda.Id, catalogo.Modo, busqueda.CodigosObjetivo.Count, busqueda.Estado);

            return ResultadoOperacion<BusquedaDto>.Exito(busqueda.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<BusquedaDto>.Falla(ex.Message);
        }
    }
}
