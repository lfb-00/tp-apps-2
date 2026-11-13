using CoreWCF;
using RepMatch.Catalogo.Soap.Contrato;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Soap;

/// <summary>
/// Implementacion del contrato SOAP. No tiene logica propia: delega en <see cref="ICatalogoRepuestos"/>
/// (enlazado a CatalogoLocal, igual que en Catalogo.Api), asi el camino SOAP devuelve exactamente lo
/// mismo que el REST para el mismo vehiculo. Es una fachada de protocolo sobre codigo ya probado.
///
/// Abre un ambito de DI por operacion: el catalogo usa el DbContext, que es scoped, y el servicio
/// SOAP vive por llamada. Asi cada operacion tiene su propio DbContext, como un request REST.
/// </summary>
[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, Namespace = Espacio.Nombre, Name = "ConsultaCompatibilidad")]
public sealed class ServicioConsultaCompatibilidad(
    IServiceScopeFactory ambitos,
    ILogger<ServicioConsultaCompatibilidad> log) : IConsultaCompatibilidad
{
    public Task<RepuestoSoap[]> ConsultarPorVehiculoAsync(
        string marca, string modelo, int anio, string? motor, string? sistema) =>
        ConCatalogoAsync(async catalogo =>
        {
            var repuestos = await catalogo.BuscarCompatiblesAsync(
                Vehiculo(marca, modelo, anio, motor), Vacio(sistema));
            log.LogInformation("SOAP ConsultarPorVehiculo {Marca} {Modelo} {Anio} {Motor} sistema={Sistema} -> {Cantidad}",
                marca, modelo, anio, motor ?? "(todos)", sistema ?? "(todos)", repuestos.Count);
            return repuestos.Select(RepuestoSoap.Desde).ToArray();
        });

    public Task<RepuestoSoap?> ObtenerPorCodigoAsync(string codigo) =>
        ConCatalogoAsync(async catalogo =>
        {
            if (string.IsNullOrWhiteSpace(codigo))
                throw new FaultException("Indicá el código del repuesto.");

            var repuesto = await catalogo.ObtenerPorCodigoAsync(codigo.Trim());
            log.LogInformation("SOAP ObtenerPorCodigo {Codigo} -> {Resultado}",
                codigo, repuesto is null ? "no encontrado" : "encontrado");
            return repuesto is null ? null : RepuestoSoap.Desde(repuesto);
        });

    public Task<RepuestoSoap[]> ListarAsync() =>
        ConCatalogoAsync(async catalogo =>
        {
            var repuestos = await catalogo.ListarAsync();
            log.LogInformation("SOAP Listar -> {Cantidad}", repuestos.Count);
            return repuestos.Select(RepuestoSoap.Desde).ToArray();
        });

    public Task<bool> VerificarCodigoAsync(string codigo, string marca, string modelo, int anio, string? motor) =>
        ConCatalogoAsync(async catalogo =>
        {
            if (string.IsNullOrWhiteSpace(codigo))
                throw new FaultException("Indicá el código del repuesto.");

            var compatibles = await catalogo.BuscarCompatiblesAsync(Vehiculo(marca, modelo, anio, motor));
            var compatible = compatibles.Any(r =>
                string.Equals(r.CodigoCanonico, codigo.Trim(), StringComparison.OrdinalIgnoreCase));
            log.LogInformation("SOAP VerificarCodigo {Codigo} {Marca} {Modelo} {Anio} -> {Compatible}",
                codigo, marca, modelo, anio, compatible);
            return compatible;
        });

    private async Task<T> ConCatalogoAsync<T>(Func<ICatalogoRepuestos, Task<T>> operacion)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        return await operacion(ambito.ServiceProvider.GetRequiredService<ICatalogoRepuestos>());
    }

    /// <summary>Un vehiculo incompleto es un error del cliente: vuelve como fault SOAP, no como 500.</summary>
    private static VehiculoDto Vehiculo(string marca, string modelo, int anio, string? motor)
    {
        if (string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo) || anio <= 0)
            throw new FaultException("Indicá marca, modelo y año del vehículo.");

        return new VehiculoDto { Marca = marca.Trim(), Modelo = modelo.Trim(), Anio = anio, Motor = Vacio(motor) };
    }

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
