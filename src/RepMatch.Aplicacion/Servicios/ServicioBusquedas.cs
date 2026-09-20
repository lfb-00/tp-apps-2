using FluentValidation;
using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Common.Resultados;
using RepMatch.Common.Validacion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Logica de negocio de las busquedas (el "Pedido" de la consigna).
///
/// Las consultas son publicas. Los pasos que mutan estado —<see cref="ValidarAsync"/> y
/// <see cref="RegistrarAsync"/>— son <c>internal</c>: la unica puerta de entrada al caso de uso
/// desde fuera de esta capa es <see cref="Fachadas.FachadaBusqueda"/>, que los invoca en orden y
/// entre uno y otro resuelve los codigos objetivo. Este servicio no sabe de donde salen esos
/// codigos —hoy del catalogo por compatibilidad, en la Segunda Parte del componente de IA—:
/// recibe la lista y aplica las reglas de registro.
/// </summary>
public sealed class ServicioBusquedas(
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
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
    /// Reglas de admision de una solicitud: bien formada segun el validador y de un cliente que
    /// existe. Es el primer paso del caso de uso, antes de cualquier consulta externa, para no
    /// gastar una llamada en algo que despues no se va a poder registrar.
    /// </summary>
    internal async Task<ResultadoOperacion> ValidarAsync(
        CrearBusquedaDto dto, CancellationToken ct = default)
    {
        var validacion = await validador.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return validacion;

        if (await clientes.ObtenerPorIdAsync(dto.ClienteId, ct) is null)
            return ResultadoOperacion.Falla($"No existe el cliente {dto.ClienteId}.");

        return ResultadoOperacion.Exito();
    }

    /// <summary>
    /// Arma el agregado, le asigna los codigos objetivo —o lo deja Fallida, con motivo, si no
    /// llego ninguno— y confirma la unidad de trabajo. Presupone una solicitud que ya paso por
    /// <see cref="ValidarAsync"/>; las invariantes del agregado se siguen verificando en el
    /// constructor de <see cref="Busqueda"/>.
    /// </summary>
    internal async Task<ResultadoOperacion<BusquedaDto>> RegistrarAsync(
        CrearBusquedaDto dto, IEnumerable<string> codigosObjetivo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(codigosObjetivo);

        try
        {
            var busqueda = new Busqueda(dto.ClienteId, dto.Vehiculo.AEntidad(), dto.TextoLibre);

            var codigos = codigosObjetivo.ToList();

            if (codigos.Count > 0)
            {
                busqueda.AsignarCodigosObjetivo(codigos);
            }
            else
            {
                busqueda.Fallar(
                    $"Todavia no tenemos repuestos catalogados para un {dto.Vehiculo}.");
            }

            await busquedas.AgregarAsync(busqueda, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogDebug("Busqueda {BusquedaId} persistida con estado {Estado}",
                busqueda.Id, busqueda.Estado);

            return ResultadoOperacion<BusquedaDto>.Exito(busqueda.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<BusquedaDto>.Falla(ex.Message);
        }
    }
}
