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
/// Colaboracion entre cliente y mecanico sobre una busqueda: el cliente la comparte, el mecanico
/// la revisa. Es un servicio aparte de <see cref="ServicioBusquedas"/> para no tocar la creacion
/// de busquedas, que es el punto de extension de la IA. Los permisos por rol se chequean aca: el
/// dominio sabe a quien esta compartida, pero no que roles existen.
/// </summary>
public sealed class ServicioRevisiones(
    IBusquedaRepository busquedas,
    IClienteRepository clientes,
    IDerivacionRepository derivaciones,
    IUnitOfWork unidadDeTrabajo,
    IDespachadorEventos despachador,
    IValidator<RevisarBusquedaDto> validadorRevision,
    ILogger<ServicioRevisiones> log)
{
    /// <summary>Detalle de una busqueda para quien la pide. Solo la ven el duenio y el mecanico
    /// al que se la compartieron; para cualquier otro no existe.</summary>
    public async Task<ConsultaDto?> ObtenerAsync(Guid busquedaId, Guid usuarioId, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(busquedaId, ct);
        if (busqueda is null || (busqueda.ClienteId != usuarioId && busqueda.MecanicoId != usuarioId))
            return null;

        return await AConsultaAsync(busqueda, ct);
    }

    public async Task<ResultadoOperacion<ConsultaDto>> CompartirAsync(
        Guid busquedaId, Guid clienteId, Guid mecanicoId, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(busquedaId, ct);
        if (busqueda is null || busqueda.ClienteId != clienteId)
            return ResultadoOperacion<ConsultaDto>.Falla("La consulta no existe o no es tuya.");

        var mecanico = await clientes.ObtenerPorIdAsync(mecanicoId, ct);
        if (mecanico is null || !mecanico.EsMecanico)
            return ResultadoOperacion<ConsultaDto>.Falla("Elegí un mecánico del directorio.");

        var pendientes = await busquedas.ContarPendientesDeRevisionAsync(mecanicoId, ct);
        if (!mecanico.PuedeRecibirBusqueda(pendientes))
        {
            log.LogInformation("Mecanico {MecanicoId} sin lugar en la bandeja ({Pendientes} pendientes, plan {Plan})",
                mecanicoId, pendientes, mecanico.Plan);
            return ResultadoOperacion<ConsultaDto>.Falla(
                "Ese mecánico tiene la bandeja llena por ahora. Probá con otro o más tarde.");
        }

        return await AplicarAsync(busqueda, b => b.CompartirCon(mecanicoId),
            $"Busqueda compartida con el mecanico {mecanicoId}", ct);
    }

    public async Task<ResultadoOperacion<ConsultaDto>> DejarDeCompartirAsync(
        Guid busquedaId, Guid clienteId, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(busquedaId, ct);
        if (busqueda is null || busqueda.ClienteId != clienteId)
            return ResultadoOperacion<ConsultaDto>.Falla("La consulta no existe o no es tuya.");

        return await AplicarAsync(busqueda, b => b.DejarDeCompartir(), "Busqueda retirada del mecanico", ct);
    }

    public async Task<ResultadoOperacion<BandejaMecanicoDto>> ObtenerBandejaAsync(
        Guid mecanicoId, CancellationToken ct = default)
    {
        var mecanico = await clientes.ObtenerPorIdAsync(mecanicoId, ct);
        if (mecanico is null || !mecanico.EsMecanico)
            return ResultadoOperacion<BandejaMecanicoDto>.Falla("La bandeja es solo para mecánicos.");

        var compartidas = await busquedas.ListarCompartidasConAsync(mecanicoId, ct);
        var nombres = await NombresDeClientesAsync(compartidas.Select(b => b.ClienteId), ct);
        var mecanicoDto = mecanico.AMecanicoDto();

        return ResultadoOperacion<BandejaMecanicoDto>.Exito(new BandejaMecanicoDto
        {
            Consultas = [.. compartidas.Select(b => new ConsultaDto
            {
                Busqueda = b.ADto(),
                NombreCliente = nombres.GetValueOrDefault(b.ClienteId, "Cliente dado de baja"),
                Mecanico = mecanicoDto
            })],
            Pendientes = compartidas.Count(b => !b.EstaRevisada),
            LimitePendientes = mecanico.LimiteBusquedasPendientes,
            DerivacionesGeneradas = await derivaciones.ContarPorMecanicoAsync(mecanicoId, ct)
        });
    }

    public async Task<ResultadoOperacion<ConsultaDto>> RevisarAsync(
        Guid busquedaId, Guid mecanicoId, RevisarBusquedaDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorRevision.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ConsultaDto>.Falla(validacion.Errores);

        var busqueda = await busquedas.ObtenerPorIdAsync(busquedaId, ct);
        if (busqueda is null || busqueda.MecanicoId != mecanicoId)
            return ResultadoOperacion<ConsultaDto>.Falla("La consulta no está en tu bandeja.");

        return await AplicarAsync(busqueda,
            b => b.RegistrarRevision(mecanicoId, dto.ConfirmaDiagnostico, dto.CodigosRecomendados, dto.Nota),
            dto.ConfirmaDiagnostico ? "Diagnostico confirmado por el mecanico" : "Diagnostico corregido por el mecanico",
            ct);
    }

    /// <summary>Aplica el cambio, confirma y recien despues despacha los eventos, igual que
    /// ServicioBusquedas: un observador nunca ve un hecho que no quedo guardado.</summary>
    private async Task<ResultadoOperacion<ConsultaDto>> AplicarAsync(
        Busqueda busqueda, Action<Busqueda> cambio, string cambioRealizado, CancellationToken ct)
    {
        try
        {
            cambio(busqueda);
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<ConsultaDto>.Falla(ex.Message);
        }

        var pendientes = busqueda.EventosDominio.ToArray();
        await unidadDeTrabajo.ConfirmarAsync(ct);
        busqueda.LimpiarEventos();
        await despachador.DespacharAsync(pendientes, ct);

        log.LogInformation("{Cambio}: busqueda {BusquedaId}", cambioRealizado, busqueda.Id);
        return ResultadoOperacion<ConsultaDto>.Exito(await AConsultaAsync(busqueda, ct));
    }

    private async Task<ConsultaDto> AConsultaAsync(Busqueda busqueda, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerPorIdAsync(busqueda.ClienteId, ct);
        var mecanico = busqueda.MecanicoId is { } id ? await clientes.ObtenerPorIdAsync(id, ct) : null;

        return new ConsultaDto
        {
            Busqueda = busqueda.ADto(),
            NombreCliente = cliente?.Nombre ?? "Cliente dado de baja",
            Mecanico = mecanico is { EsMecanico: true } ? mecanico.AMecanicoDto() : null
        };
    }

    private async Task<Dictionary<Guid, string>> NombresDeClientesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var nombres = new Dictionary<Guid, string>();
        foreach (var id in ids.Distinct())
        {
            if (await clientes.ObtenerPorIdAsync(id, ct) is { } cliente)
                nombres[id] = cliente.Nombre;
        }

        return nombres;
    }
}
