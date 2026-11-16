using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Una casa de repuestos del mapa de OpenStreetMap, copiada a la base propia. Se copia en vez de
/// consultarla en cada busqueda porque los servidores publicos de Overpass se saturan seguido
/// ("at capacity"): el worker la importa una vez y la refresca cada semana, y la busqueda de
/// casas cercanas lee solo la base. La licencia de OpenStreetMap (ODbL) permite guardar la copia
/// con la atribucion, que la pantalla muestra.
/// </summary>
public class CasaRepuestos : EntidadBase
{
    private CasaRepuestos() { }   // requerido por EF Core

    public CasaRepuestos(
        string idMapa, string nombre, double latitud, double longitud, string? direccion, string? telefono,
        DateTimeOffset importadaEn)
    {
        ExcepcionDominio.SiNulaOVacia(idMapa, nameof(idMapa));
        ExcepcionDominio.SiNulaOVacia(nombre, nameof(nombre));
        ExcepcionDominio.Si(latitud is < -90 or > 90 || longitud is < -180 or > 180, "Coordenadas fuera de rango.");

        IdMapa = idMapa.Trim();
        Nombre = nombre.Trim();
        Latitud = latitud;
        Longitud = longitud;
        Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        ImportadaEn = importadaEn;
    }

    /// <summary>El elemento en OpenStreetMap, "node/4557873526" o "way/123": arma el enlace a su ficha.</summary>
    public string IdMapa { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }
    public string? Direccion { get; private set; }
    public string? Telefono { get; private set; }
    public DateTimeOffset ImportadaEn { get; private set; }
}
