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
/// Logica de negocio de clientes y su garage. Vive en la capa intermedia: la presentacion no
/// toca repositorios y la persistencia no conoce reglas de negocio.
/// </summary>
public sealed class ServicioClientes(
    IClienteRepository clientes,
    IUnitOfWork unidadDeTrabajo,
    IValidator<CrearClienteDto> validador,
    ILogger<ServicioClientes> log)
{
    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default)
    {
        var todos = await clientes.ListarAsync(ct);
        return [.. todos.Select(c => c.ADto())];
    }

    public async Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerPorIdAsync(id, ct);
        return cliente?.ADto();
    }

    public async Task<ClienteDto?> ObtenerPorEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var cliente = await clientes.ObtenerPorEmailAsync(email, ct);
        return cliente?.ADto();
    }

    public async Task<ResultadoOperacion<ClienteDto>> RegistrarAsync(
        CrearClienteDto dto, CancellationToken ct = default)
    {
        var validacion = await validador.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ClienteDto>.Falla(validacion.Errores);

        if (await clientes.ObtenerPorEmailAsync(dto.Email, ct) is not null)
            return ResultadoOperacion<ClienteDto>.Falla($"Ya existe un cliente con el email {dto.Email}.");

        try
        {
            var cliente = new Cliente(dto.Nombre, dto.Email);
            cliente.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword(dto.Contrasena));
            await clientes.AgregarAsync(cliente, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogInformation("Cliente registrado {ClienteId} ({Email})", cliente.Id, cliente.Email);
            return ResultadoOperacion<ClienteDto>.Exito(cliente.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<ClienteDto>.Falla(ex.Message);
        }
    }

    public async Task<ClienteDto?> AutenticarAsync(string email, string contrasena, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(contrasena))
            return null;

        var cliente = await clientes.ObtenerPorEmailAsync(email, ct);
        if (cliente is null || cliente.HashContrasena is null)
            return null;

        return BCrypt.Net.BCrypt.Verify(contrasena, cliente.HashContrasena)
            ? cliente.ADto()
            : null;
    }

    public async Task<ResultadoOperacion<ClienteDto>> AgregarVehiculoAsync(
        Guid clienteId, VehiculoDto vehiculo, string? vin = null, string? alias = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var cliente = await clientes.ObtenerPorIdAsync(clienteId, ct);
        if (cliente is null)
            return ResultadoOperacion<ClienteDto>.Falla($"No existe el cliente {clienteId}.");

        try
        {
            cliente.AgregarVehiculo(vehiculo.AEntidad(), vin, alias);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogInformation("Vehiculo {Vehiculo} agregado al cliente {ClienteId}", vehiculo, clienteId);
            return ResultadoOperacion<ClienteDto>.Exito(cliente.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<ClienteDto>.Falla(ex.Message);
        }
    }
}
