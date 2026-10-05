namespace RepMatch.Proveedores.Configuracion;

/// <summary>
/// Configuracion externalizada de los proveedores de ofertas. Se sobreescribe por variable de
/// entorno (Proveedores__MercadoLibre__Habilitado=false) sin recompilar.
/// </summary>
public sealed class OpcionesProveedores
{
    public const string Seccion = "Proveedores";

    public OpcionesMercadoLibre MercadoLibre { get; set; } = new();
    public OpcionesEbay eBay { get; set; } = new();
}

public sealed class OpcionesMercadoLibre
{
    /// <summary>Desactivar en tests o cuando la API no está disponible.</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>
    /// App ID de la aplicación de MercadoLibre. Opcional para búsquedas de lectura pública,
    /// pero aumenta el rate limit. Registrarse gratis en developers.mercadolibre.com.ar.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>Máximo de resultados por búsqueda (la API admite hasta 50).</summary>
    public int LimitePorBusqueda { get; set; } = 5;
}

public sealed class OpcionesEbay
{
    /// <summary>Desactivar si no se configuraron credenciales.</summary>
    public bool Habilitado { get; set; } = false;

    /// <summary>Client ID de la app registrada en developer.ebay.com.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Client Secret de la app registrada en developer.ebay.com.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>Máximo de resultados por búsqueda.</summary>
    public int LimitePorBusqueda { get; set; } = 5;
}
