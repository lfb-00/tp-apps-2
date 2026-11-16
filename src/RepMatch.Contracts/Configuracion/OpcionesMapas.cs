using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>
/// Seccion "Mapas": de donde salen las casas de repuestos cercanas. Por defecto, las APIs publicas
/// de OpenStreetMap, sin credenciales. Sus politicas de uso piden identificarse con un User-Agent
/// propio, no superar un pedido por segundo en Nominatim y cachear: por eso
/// <see cref="CacheHoras"/> y el limite de un pedido a la vez.
/// </summary>
public sealed class OpcionesMapas
{
    public const string Seccion = "Mapas";

    /// <summary>
    /// Apagado por defecto y encendido en compose (<c>MAPAS_HABILITADO</c>). La busqueda lee la
    /// copia de casas que mantiene el worker: con <c>dotnet run</c> de la Web sola no hay worker,
    /// la copia nunca se carga y la seccion responderia "todavia estamos cargando" para siempre.
    /// </summary>
    public bool Habilitado { get; set; }

    /// <summary>Servidor de Overpass (comercios por tipo alrededor de un punto).</summary>
    public string UrlOverpass { get; set; } = "https://overpass-api.de/api/";

    /// <summary>Servidor de Nominatim (zona escrita a coordenadas).</summary>
    public string UrlNominatim { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>Identifica a la aplicacion ante OpenStreetMap, como pide su politica de uso. No
    /// lleva datos de ninguna persona.</summary>
    public string AgenteUsuario { get; set; } = "RepMatch/1.0 (comparador de repuestos, TP UADE)";

    [Range(500, 20000)]
    public int RadioMetros { get; set; } = 5000;

    [Range(1, 50)]
    public int Maximo { get; set; } = 10;

    /// <summary>Para ubicar una zona escrita (Nominatim), que se hace mientras la persona espera.</summary>
    [Range(1, 60)]
    public int TimeoutSegundos { get; set; } = 10;

    /// <summary>Las zonas no se mueven: un dia de cache le ahorra pedidos al servidor publico.</summary>
    [Range(0, 168)]
    public int CacheHoras { get; set; } = 24;

    /// <summary>La descarga de todas las casas de Argentina: un pedido grande, en segundo plano.</summary>
    [Range(30, 900)]
    public int TimeoutDescargaSegundos { get; set; } = 300;

    /// <summary>Cada cuanto el worker vuelve a descargar las casas. Los comercios cambian poco.</summary>
    [Range(1, 60)]
    public int RefrescoDias { get; set; } = 7;

    /// <summary>Si la descarga falla (Overpass saturado), cuanto esperar para reintentar.</summary>
    [Range(1, 1440)]
    public int ReintentoMinutos { get; set; } = 15;
}
