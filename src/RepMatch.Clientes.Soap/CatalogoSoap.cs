using System.ServiceModel;
using Microsoft.Extensions.Logging;
using RepMatch.Clientes.Soap.ServicioSoap;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Clientes.Soap;

/// <summary>
/// Acceso por SOAP al componente de catalogo: la tercera implementacion de
/// <see cref="ICatalogoRepuestos"/>, junto a CatalogoLocal (en proceso) y CatalogoRemoto (REST).
/// Ninguna pagina ni servicio de negocio cambio para sumarla: es lo que el diseno del TP Inicial
/// prometia (Strategy elegida por configuracion).
///
/// Es un Adapter en dos sentidos. Traduce los tipos del proxy generado desde el WSDL a los DTO del
/// contrato interno. Y traduce los errores de WCF (EndpointNotFoundException, TimeoutException,
/// faults) a <see cref="HttpRequestException"/>, que es lo que la fachada y el observador ya
/// atrapan cuando falla el catalogo remoto: sin esa traduccion, una caida del servicio SOAP tiraria
/// la pagina en vez de mostrar el mensaje de "no se pudo consultar el catalogo".
/// </summary>
public sealed class CatalogoSoap(FabricaCanalSoap fabrica, ILogger<CatalogoSoap> log) : ICatalogoRepuestos
{
    public string Modo => "Soap";

    public async Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var repuestos = await LlamarAsync(canal => canal.ConsultarPorVehiculoAsync(
            vehiculo.Marca, vehiculo.Modelo, vehiculo.Anio, vehiculo.Motor!, sistema!), ct);

        log.LogInformation(
            "Catalogo[{Modo}] compatibilidad {Vehiculo} sistema={Sistema} -> {Cantidad} repuestos",
            Modo, vehiculo, sistema ?? "(todos)", repuestos.Length);

        return [.. repuestos.Select(ADto)];
    }

    public async Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoCanonico);

        var repuesto = await LlamarAsync(canal => canal.ObtenerPorCodigoAsync(codigoCanonico), ct);

        log.LogInformation("Catalogo[{Modo}] codigo={Codigo} -> {Resultado}",
            Modo, codigoCanonico, repuesto is null ? "no encontrado" : "encontrado");

        return repuesto is null ? null : ADto(repuesto);
    }

    public async Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default)
    {
        var repuestos = await LlamarAsync(canal => canal.ListarAsync(), ct);

        log.LogInformation("Catalogo[{Modo}] listado completo -> {Cantidad} repuestos", Modo, repuestos.Length);
        return [.. repuestos.Select(ADto)];
    }

    /// <summary>
    /// Un canal por llamada, sobre la fabrica compartida: cerrarlo si salio bien, abortarlo si no
    /// (cerrar un canal fallado tira otra excepcion). El CancellationToken no llega al proxy
    /// generado; el limite lo pone el timeout del binding.
    /// </summary>
    private async Task<T> LlamarAsync<T>(Func<ConsultaCompatibilidadChannel, Task<T>> operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var canal = fabrica.CrearCanal();
        try
        {
            var resultado = await operacion(canal);
            await Task.Factory.FromAsync(canal.BeginClose, canal.EndClose, null);
            return resultado;
        }
        catch (Exception ex) when (ex is CommunicationException or TimeoutException)
        {
            canal.Abort();
            log.LogWarning("Catalogo[{Modo}] fallo la llamada SOAP: {Tipo}: {Mensaje}", Modo, ex.GetType().Name, ex.Message);
            throw new HttpRequestException($"El servicio SOAP del catálogo no respondió: {ex.Message}", ex);
        }
        catch
        {
            canal.Abort();
            throw;
        }
    }

    private static RepuestoDto ADto(Repuesto r) => new()
    {
        Id = r.Id,
        CodigoCanonico = r.CodigoCanonico,
        Nombre = r.Nombre,
        Descripcion = r.Descripcion,
        Sistema = r.Sistema,
        CodigosEquivalentes = [.. r.CodigosEquivalentes ?? []],
        Aplicaciones = [.. (r.Aplicaciones ?? []).Select(a => new AplicacionDto
        {
            Marca = a.Marca,
            Modelo = a.Modelo,
            AnioDesde = a.AnioDesde,
            AnioHasta = a.AnioHasta,
            Motor = a.Motor
        })]
    };
}
