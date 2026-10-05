namespace RepMatch.Contracts.Dtos;

/// <summary>
/// Oferta normalizada que devuelve un IProveedorOfertas. Es el DTO de frontera entre los
/// adaptadores de tiendas externas y la capa de aplicacion, que la convierte en una entidad
/// Oferta del dominio antes de persistirla.
/// </summary>
public sealed record OfertaExternaDto(
    string CodigoRepuesto,
    string Titulo,
    decimal Precio,
    string Moneda,
    string Url,
    string NombreTienda,
    decimal? CostoEnvio = null,
    bool Disponible = true);
