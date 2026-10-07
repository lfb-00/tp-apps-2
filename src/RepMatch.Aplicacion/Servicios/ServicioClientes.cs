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
    IBusquedaRepository busquedas,
    IUnitOfWork unidadDeTrabajo,
    IValidator<CrearClienteDto> validador,
    IValidator<VehiculoDto> validadorVehiculo,
    IValidator<ActualizarPerfilDto> validadorPerfil,
    IValidator<CambiarContrasenaDto> validadorContrasena,
    IValidator<CambiarFotoPerfilDto> validadorFoto,
    IValidator<EliminarCuentaDto> validadorEliminar,
    IConfiguracionVehiculoRepository configuracionesVehiculo,
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

        var validacion = await validadorVehiculo.ValidarAsync(vehiculo, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ClienteDto>.Falla(validacion.Errores);

        if (!await configuracionesVehiculo.ExisteAsync(vehiculo.Marca, vehiculo.Modelo, vehiculo.Anio, vehiculo.Motor, ct))
            return ResultadoOperacion<ClienteDto>.Falla("Elegí una configuración válida del catálogo de vehículos.");

        if (alias?.Trim().Length > Vehiculo.LongitudMaximaAlias)
            return ResultadoOperacion<ClienteDto>.Falla("El apodo admite hasta 120 caracteres.");

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

    // Nunca se loguean contraseñas, bytes de imagen ni el telefono: solo el id del cliente.

    public async Task<ResultadoOperacion<ClienteDto>> ActualizarPerfilAsync(
        Guid clienteId, ActualizarPerfilDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorPerfil.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ClienteDto>.Falla(validacion.Errores);

        return await ModificarAsync(clienteId, cliente =>
        {
            cliente.CambiarNombre(dto.Nombre);
            cliente.ActualizarContacto(dto.Telefono, dto.TieneWhatsApp, dto.Provincia, dto.Localidad);
        }, "Perfil actualizado", ct);
    }

    public async Task<ResultadoOperacion> CambiarContrasenaAsync(
        Guid clienteId, CambiarContrasenaDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorContrasena.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return validacion;

        var cliente = await clientes.ObtenerPorIdAsync(clienteId, ct);
        if (cliente is null)
            return ResultadoOperacion.Falla($"No existe el cliente {clienteId}.");

        if (!ContrasenaCorrecta(cliente, dto.ContrasenaActual))
        {
            log.LogWarning("Cambio de contraseña rechazado para {ClienteId}: la actual no coincide", clienteId);
            return ResultadoOperacion.Falla("La contraseña actual es incorrecta.");
        }

        cliente.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword(dto.ContrasenaNueva));
        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation("Contraseña cambiada del cliente {ClienteId}", clienteId);
        return ResultadoOperacion.Exito();
    }

    public async Task<ResultadoOperacion<ClienteDto>> CambiarFotoPerfilAsync(
        Guid clienteId, CambiarFotoPerfilDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorFoto.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return ResultadoOperacion<ClienteDto>.Falla(validacion.Errores);

        return await ModificarAsync(clienteId, cliente => cliente.CambiarFotoPerfil(dto.Datos, dto.TipoContenido),
            "Foto de perfil cambiada", ct);
    }

    public Task<ResultadoOperacion<ClienteDto>> QuitarFotoPerfilAsync(
        Guid clienteId, CancellationToken ct = default) =>
        ModificarAsync(clienteId, cliente => cliente.QuitarFotoPerfil(),
            "Foto de perfil quitada", ct);

    public Task<ResultadoOperacion<ClienteDto>> EstablecerVehiculoPredeterminadoAsync(
        Guid clienteId, Guid? vehiculoId, CancellationToken ct = default) =>
        ModificarAsync(clienteId, cliente => cliente.EstablecerVehiculoPredeterminado(vehiculoId),
            "Vehiculo predeterminado cambiado", ct);

    public Task<ResultadoOperacion<ClienteDto>> CambiarTemaAsync(
        Guid clienteId, string tema, CancellationToken ct = default) =>
        ModificarAsync(clienteId, cliente => cliente.CambiarTema(tema),
            "Tema cambiado", ct);

    /// <summary>
    /// Borra el cliente, su garage (cascada en la base) y sus busquedas (que no tienen FK a
    /// clientes) en una sola transaccion. Pide la contraseña porque no se puede deshacer.
    /// </summary>
    public async Task<ResultadoOperacion> EliminarCuentaAsync(
        Guid clienteId, EliminarCuentaDto dto, CancellationToken ct = default)
    {
        var validacion = await validadorEliminar.ValidarAsync(dto, ct);
        if (validacion.EsFallido)
            return validacion;

        var cliente = await clientes.ObtenerPorIdAsync(clienteId, ct);
        if (cliente is null)
            return ResultadoOperacion.Falla($"No existe el cliente {clienteId}.");

        if (!ContrasenaCorrecta(cliente, dto.Contrasena))
        {
            log.LogWarning("Eliminacion de cuenta rechazada para {ClienteId}: contraseña incorrecta", clienteId);
            return ResultadoOperacion.Falla("La contraseña es incorrecta.");
        }

        var cantidadVehiculos = cliente.Vehiculos.Count;
        var cantidadBusquedas = 0;

        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            // clientes -> vehiculo predeterminado y vehiculos -> cliente forman un ciclo: EF no
            // puede ordenar el DELETE de los dos en un solo guardado. Se suelta primero la FK del
            // predeterminado, dentro de la misma transaccion.
            if (cliente.VehiculoPredeterminadoId is not null)
            {
                cliente.EstablecerVehiculoPredeterminado(null);
                await unidadDeTrabajo.ConfirmarAsync(token);
            }

            cantidadBusquedas = await busquedas.EliminarPorClienteAsync(clienteId, token);
            clientes.Eliminar(cliente);
            await unidadDeTrabajo.ConfirmarAsync(token);
        }, ct);

        log.LogInformation("Cuenta eliminada {ClienteId}: {Vehiculos} vehiculos y {Busquedas} busquedas",
            clienteId, cantidadVehiculos, cantidadBusquedas);
        return ResultadoOperacion.Exito();
    }

    private static bool ContrasenaCorrecta(Cliente cliente, string contrasena) =>
        cliente.HashContrasena is not null && BCrypt.Net.BCrypt.Verify(contrasena, cliente.HashContrasena);

    /// <summary>Carga el cliente, aplica el cambio de dominio y confirma. Una invariante violada
    /// vuelve como falla, no como excepcion.</summary>
    private async Task<ResultadoOperacion<ClienteDto>> ModificarAsync(
        Guid clienteId, Action<Cliente> cambio, string cambioRealizado, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerPorIdAsync(clienteId, ct);
        if (cliente is null)
            return ResultadoOperacion<ClienteDto>.Falla($"No existe el cliente {clienteId}.");

        try
        {
            cambio(cliente);
            await unidadDeTrabajo.ConfirmarAsync(ct);

            log.LogInformation("{Cambio} del cliente {ClienteId}", cambioRealizado, clienteId);
            return ResultadoOperacion<ClienteDto>.Exito(cliente.ADto());
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<ClienteDto>.Falla(ex.Message);
        }
    }
}
