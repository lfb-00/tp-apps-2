using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Entidades;

/// <summary>Combinación de marca, modelo, rango de años y motor que la UI permite seleccionar.</summary>
public sealed class ConfiguracionVehiculo : EntidadBase
{
    private ConfiguracionVehiculo() { }

    public ConfiguracionVehiculo(string marca, string modelo, int anioDesde, int anioHasta, string motor)
    {
        ExcepcionDominio.SiNulaOVacia(marca, nameof(marca));
        ExcepcionDominio.SiNulaOVacia(modelo, nameof(modelo));
        ExcepcionDominio.SiNulaOVacia(motor, nameof(motor));
        ExcepcionDominio.Si(marca.Trim().Length > 50, "La marca admite hasta 50 caracteres.");
        ExcepcionDominio.Si(modelo.Trim().Length > 50, "El modelo admite hasta 50 caracteres.");
        ExcepcionDominio.Si(motor.Trim().Length > 50, "El motor admite hasta 50 caracteres.");
        ExcepcionDominio.Si(anioDesde < 1950 || anioHasta > DateTime.UtcNow.Year + 1 || anioDesde > anioHasta,
            "El rango de años de la configuración es inválido.");

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        AnioDesde = anioDesde;
        AnioHasta = anioHasta;
        Motor = motor.Trim();
    }

    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public int AnioDesde { get; private set; }
    public int AnioHasta { get; private set; }
    public string Motor { get; private set; } = string.Empty;

    public bool Incluye(string marca, string modelo, int anio, string? motor) =>
        Marca.Equals(marca?.Trim(), StringComparison.OrdinalIgnoreCase)
        && Modelo.Equals(modelo?.Trim(), StringComparison.OrdinalIgnoreCase)
        && anio >= AnioDesde && anio <= AnioHasta
        && Motor.Equals(motor?.Trim(), StringComparison.OrdinalIgnoreCase);
}
