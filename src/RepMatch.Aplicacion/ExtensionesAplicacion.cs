using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Fachada;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Aplicacion.Validadores;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Aplicacion;

/// <summary>
/// Registro de la capa de logica de negocio. Deliberadamente NO registra
/// <see cref="Contracts.ICatalogoRepuestos"/>: esa eleccion es del host, que decide con
/// FabricaCatalogo si lo resuelve en proceso o por HTTP.
/// </summary>
public static class ExtensionesAplicacion
{
    public static IServiceCollection AgregarAplicacion(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<IValidator<CrearClienteDto>, ValidadorCrearCliente>();
        servicios.AddScoped<IValidator<CrearBusquedaDto>, ValidadorCrearBusqueda>();
        servicios.AddScoped<IValidator<VehiculoDto>, ValidadorVehiculo>();
        servicios.AddScoped<IValidator<ActualizarPerfilDto>, ValidadorActualizarPerfil>();
        servicios.AddScoped<IValidator<CambiarContrasenaDto>, ValidadorCambiarContrasena>();
        servicios.AddScoped<IValidator<CambiarFotoPerfilDto>, ValidadorCambiarFotoPerfil>();
        servicios.AddScoped<IValidator<EliminarCuentaDto>, ValidadorEliminarCuenta>();

        servicios.AddScoped<ServicioClientes>();
        servicios.AddScoped<ServicioBusquedas>();
        servicios.AddScoped<FachadaAplicacion>();

        servicios.AddScoped<IObservadorEventoDominio, ObservadorCompatibilidad>();
        // ObservadorOfertas se registra segundo: el despachador lo ejecuta despues de
        // ObservadorCompatibilidad, cuando CodigosObjetivo ya fueron asignados y persistidos.
        servicios.AddScoped<IObservadorEventoDominio, ObservadorOfertas>();
        servicios.AddScoped<IDespachadorEventos, DespachadorEventos>();

        return servicios;
    }
}
