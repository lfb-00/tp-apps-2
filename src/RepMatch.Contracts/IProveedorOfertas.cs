using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>
/// Contrato de un proveedor de ofertas externas. Cada implementacion consulta una tienda o
/// marketplace real (MercadoLibre, eBay, ...) y normaliza la respuesta al mismo DTO.
///
/// La pluralidad de proveedores es el mecanismo que hace posible la comparacion: el observador
/// llama a todos en paralelo y la presentacion ordena los resultados por precio total.
/// </summary>
public interface IProveedorOfertas
{
    /// <summary>Nombre de la fuente que aparece en la tarjeta de oferta ("MercadoLibre", "eBay").</summary>
    string Nombre { get; }

    /// <summary>
    /// Busca publicaciones para el codigo de repuesto indicado, restringiendo por vehiculo para
    /// mejorar la relevancia. Devuelve lista vacia si no encuentra nada; nunca lanza excepciones
    /// de negocio: los errores de red o de la API externa se registran y se retorna vacio.
    /// </summary>
    Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(
        string codigoRepuesto,
        VehiculoDto vehiculo,
        CancellationToken ct = default);
}
