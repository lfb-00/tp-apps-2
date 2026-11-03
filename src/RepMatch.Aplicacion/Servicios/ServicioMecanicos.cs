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
/// El lado profesional de la cuenta: directorio de mecanicos, datos del taller y plan. El plan se
/// cambia sin cobrar: en el modelo de negocio el pago lo procesa un proveedor externo y RepMatch
/// solo aplica los limites, asi que el TP no implementa facturacion.
/// </summary>
public sealed class ServicioMecanicos(
    IClienteRepository clientes,
    IUnitOfWork unidadDeTrabajo,
    IValidator<ActualizarTallerDto> validadorTaller,
    ILogger<ServicioMecanicos> log)
{
    public async Task<IReadOnlyList<MecanicoDto>> ListarAsync(
        string? provincia = null, CancellationToken ct = default)
    {
        var mecanicos = await clientes.ListarMecanicosAsync(provincia, ct);
        return [.. mecanicos.Select(m => m.AMecanicoDto())];
    }

    public async Task<ResultadoOperacion<ClienteDto>> ActualizarTallerAsync(
        Guid mecanicoId, ActualizarTallerDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorTaller.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ClienteDto>.Falla(validacion.Errores);

        var especialidades = dto.Especialidades.Select(Mapeadores.ASistema).OfType<SistemaVehiculo>();

        return await ModificarAsync(mecanicoId, m => m.ActualizarTaller(dto.NombreTaller, especialidades),
            "Datos del taller actualizados", ct);
    }

    public Task<ResultadoOperacion<ClienteDto>> CambiarPlanAsync(
        Guid mecanicoId, string plan, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PlanMecanico>(plan, ignoreCase: true, out var nuevo) || !Enum.IsDefined(nuevo))
            return Task.FromResult(ResultadoOperacion<ClienteDto>.Falla($"El plan '{plan}' no existe."));

        return ModificarAsync(mecanicoId, m => m.CambiarPlan(nuevo), $"Plan cambiado a {nuevo}", ct);
    }

    private async Task<ResultadoOperacion<ClienteDto>> ModificarAsync(
        Guid mecanicoId, Action<Cliente> cambio, string cambioRealizado, CancellationToken ct)
    {
        var mecanico = await clientes.ObtenerPorIdAsync(mecanicoId, ct);
        if (mecanico is null)
            return ResultadoOperacion<ClienteDto>.Falla($"No existe el usuario {mecanicoId}.");

        try
        {
            cambio(mecanico);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogInformation("{Cambio} del mecanico {MecanicoId}", cambioRealizado, mecanicoId);
            return ResultadoOperacion<ClienteDto>.Exito(mecanico.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<ClienteDto>.Falla(ex.Message);
        }
    }
}
