using RepMatch.Contracts.Dtos;

namespace RepMatch.Contracts;

/// <summary>
/// El mapa de comercios del que salen las casas de repuestos: descargar todas las de Argentina y
/// ubicar una zona escrita a mano ("Palermo", "Rosario"). Es la segunda API publica que usa RepMatch
/// ademas de las tiendas: cuando una pieza no tiene ofertas en linea, la persona puede comprarla en
/// persona.
///
/// La implementa <c>MapaOpenStreetMap</c> (RepMatch.Fuentes.Mapas) con las APIs publicas de
/// OpenStreetMap: Overpass para los comercios y Nominatim para las zonas. Overpass no se consulta en
/// cada busqueda: sus servidores publicos se saturan seguido, asi que el worker descarga las casas
/// una vez por semana a la base propia y la busqueda de cercanas lee solo la base. Otro proveedor
/// seria otra implementacion, sin tocar a quien la usa. Sin configurar responde
/// <c>MapaDesactivado</c>, y la pantalla no muestra la seccion.
/// </summary>
public interface IMapaCasasRepuestos
{
    /// <summary>false si no hay mapa configurado: la pantalla no ofrece buscar comercios.</summary>
    bool EstaDisponible { get; }

    /// <summary>Todas las casas de repuestos de Argentina (sin distancia: no hay centro). Es un pedido
    /// pesado; lo hace el worker. Si el proveedor no responde lanza <see cref="HttpRequestException"/>.</summary>
    Task<IReadOnlyList<CasaRepuestosDto>> DescargarCasasAsync(CancellationToken ct = default);

    /// <summary>
    /// Una copia incluida en la aplicacion, con la fecha en que se descargo, para el primer arranque
    /// con la base vacia y el proveedor caido. null si no hay copia.
    /// </summary>
    (IReadOnlyList<CasaRepuestosDto> Casas, DateTimeOffset Fecha)? CopiaIncluida();

    /// <summary>El centro de una zona de Argentina escrita a mano. null si no la encuentra.</summary>
    Task<UbicacionDto?> UbicarAsync(string zona, CancellationToken ct = default);
}
