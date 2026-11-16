namespace RepMatch.Contracts.Dtos;

/// <summary>Un punto del mapa. <see cref="Descripcion"/> es lo que encontro el mapa para una zona
/// escrita ("Palermo, Buenos Aires"); null si vino del navegador.</summary>
public sealed record UbicacionDto
{
    public required double Latitud { get; init; }
    public required double Longitud { get; init; }
    public string? Descripcion { get; init; }

    /// <summary>Dentro de los limites del planeta: lo que manda el navegador no se da por bueno.</summary>
    public bool EsValida =>
        double.IsFinite(Latitud) && double.IsFinite(Longitud)
        && Latitud is >= -90 and <= 90 && Longitud is >= -180 and <= 180;
}

/// <summary>Una casa de repuestos del mapa. Direccion y telefono son null si nadie los cargo.
/// <see cref="IdMapa"/> es el elemento en OpenStreetMap ("node/123" o "way/456").</summary>
public sealed record CasaRepuestosDto
{
    public required string IdMapa { get; init; }
    public required string Nombre { get; init; }
    public required double Latitud { get; init; }
    public required double Longitud { get; init; }

    /// <summary>Desde el centro de la busqueda, en linea recta. 0 en la descarga, que no tiene centro.</summary>
    public int DistanciaMetros { get; init; }

    /// <summary>
    /// Las coordenadas en Google Maps (formato "Maps URLs", publico y sin clave): un pin en el
    /// comercio y, desde ahi, como llegar en la app que la persona ya usa. Se usan las coordenadas y
    /// no el nombre porque el nombre que cargo la comunidad de OpenStreetMap puede no coincidir con
    /// el de Google y llevar a otro lugar.
    /// </summary>
    public string UrlMapa => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"https://www.google.com/maps/search/?api=1&query={Latitud:0.######},{Longitud:0.######}");

    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
}

/// <summary>Lo que muestra la seccion: el centro usado y los comercios cerca.</summary>
public sealed record CasasCercanasDto
{
    public required UbicacionDto Centro { get; init; }
    public IReadOnlyList<CasaRepuestosDto> Casas { get; init; } = [];
    public int RadioMetros { get; init; }
}
