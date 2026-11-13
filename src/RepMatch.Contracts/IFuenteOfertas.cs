using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>
/// Una tienda externa donde buscar precios de una pieza del catalogo. Cada implementacion es un
/// Adapter de la API publica de esa tienda (nunca scraping) y devuelve las ofertas ya
/// normalizadas: el resto de la aplicacion no conoce el formato de ninguna tienda.
///
/// Es la pieza que pide la Segunda Parte como "consumo de al menos una API externa real". Hoy la
/// implementan las tiendas VTEX (easy.com.ar y fravega.com, issue #18); eBay (#17) seria otra
/// implementacion mas, sin tocar a <c>ServicioOfertas</c> ni a la interfaz.
/// </summary>
public interface IFuenteOfertas
{
    /// <summary>Nombre de la tienda tal como se le muestra a la persona ("Easy").</summary>
    string Nombre { get; }

    /// <summary>
    /// Ofertas de la tienda para una pieza. Si la tienda no responde, lanza
    /// <see cref="HttpRequestException"/> o <see cref="TaskCanceledException"/>: quien la llama
    /// decide como seguir sin ella. Si responde sin resultados, devuelve una lista vacia.
    /// </summary>
    Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default);
}
