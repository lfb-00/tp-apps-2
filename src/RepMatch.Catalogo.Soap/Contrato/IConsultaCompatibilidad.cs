using CoreWCF;

namespace RepMatch.Catalogo.Soap.Contrato;

/// <summary>
/// Contrato SOAP del catalogo: la consigna de la Segunda Parte pide al menos un servicio SOAP con
/// WSDL y contrato. Es el mismo caso de uso que Catalogo.Api expone por REST (que repuestos le van
/// a un vehiculo), publicado con un contrato formal: el WSDL que genera este servicio es lo unico
/// que necesita un cliente para usarlo, sin compartir codigo (RepMatch.Clientes.Soap lo demuestra
/// generando su proxy con dotnet-svcutil).
///
/// Las tres primeras operaciones cubren <c>ICatalogoRepuestos</c> completo, para que el cliente
/// SOAP pueda ser una tercera implementacion intercambiable con la local y la REST.
/// <see cref="VerificarCodigoAsync"/> es propia de SOAP: responde si una pieza le va a un vehiculo
/// sin transferir el catalogo, el tipo de consulta puntual que hace otro sistema (un taller, un
/// proveedor) cuando se integra por contrato.
/// </summary>
[ServiceContract(Namespace = Espacio.Nombre, Name = "ConsultaCompatibilidad")]
public interface IConsultaCompatibilidad
{
    /// <summary>Repuestos compatibles con el vehiculo, opcionalmente de un sistema (Frenos, Motor...).</summary>
    [OperationContract(Name = "ConsultarPorVehiculo")]
    Task<RepuestoSoap[]> ConsultarPorVehiculoAsync(string marca, string modelo, int anio, string? motor, string? sistema);

    /// <summary>El repuesto con ese codigo canonico, o null si no existe.</summary>
    [OperationContract(Name = "ObtenerPorCodigo")]
    Task<RepuestoSoap?> ObtenerPorCodigoAsync(string codigo);

    /// <summary>El catalogo completo.</summary>
    [OperationContract(Name = "Listar")]
    Task<RepuestoSoap[]> ListarAsync();

    /// <summary>Si el repuesto con ese codigo le va al vehiculo.</summary>
    [OperationContract(Name = "VerificarCodigo")]
    Task<bool> VerificarCodigoAsync(string codigo, string marca, string modelo, int anio, string? motor);
}

/// <summary>Espacio de nombres XML del contrato. Cambiarlo es cambiar el contrato publicado.</summary>
public static class Espacio
{
    public const string Nombre = "urn:repmatch:catalogo:v1";
}
