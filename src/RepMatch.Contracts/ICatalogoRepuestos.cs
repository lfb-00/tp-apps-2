using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>
/// Contrato del componente de catalogo, independiente de donde se ejecute.
///
/// Es el eje del ejercicio de acceso local vs. remoto que pide el TP Inicial: la capa de
/// presentacion depende solo de esta interfaz, y una Factory decide en tiempo de arranque si
/// resolverla con una llamada directa en proceso o con una llamada HTTP a Catalogo.Api. El
/// codigo que la consume no cambia ni una linea entre un modo y el otro.
///
/// En la Segunda Parte se agrega una tercera implementacion sobre SOAP (CoreWCF) sin tocar
/// ni la interfaz ni sus consumidores.
/// </summary>
public interface ICatalogoRepuestos
{
    /// <summary>Como se esta accediendo al componente ("Local", "Remoto", ...). Se registra en el
    /// log de cada operacion: es la evidencia que pide el entregable.</summary>
    string Modo { get; }

    /// <summary>Repuestos del catalogo compatibles con el vehiculo indicado.</summary>
    /// <param name="sistema">Filtro opcional por sistema (Frenos, Motor, ...).</param>
    Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
        VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default);

    Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default);

    Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default);
}
