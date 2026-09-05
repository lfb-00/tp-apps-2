using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
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

        servicios.AddScoped<ServicioClientes>();
        servicios.AddScoped<ServicioBusquedas>();

        return servicios;
    }
}
