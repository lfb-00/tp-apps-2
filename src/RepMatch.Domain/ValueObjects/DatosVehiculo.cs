using RepMatch.Domain.Comun;

namespace RepMatch.Domain.ValueObjects;

/// <summary>
/// Identificacion minima de un vehiculo. Es un value object: se compara por valor y se guarda
/// como snapshot dentro de una Busqueda, para que la busqueda siga siendo interpretable aunque
/// el cliente despues borre el vehiculo de su garage.
/// </summary>
public sealed record DatosVehiculo
{
    public const int AnioMinimo = 1950;

    public string Marca { get; }
    public string Modelo { get; }
    public int Anio { get; }
    public string? Motor { get; }

    public DatosVehiculo(string marca, string modelo, int anio, string? motor = null)
    {
        ExcepcionDominio.SiNulaOVacia(marca, nameof(marca));
        ExcepcionDominio.SiNulaOVacia(modelo, nameof(modelo));
        ExcepcionDominio.Si(anio < AnioMinimo || anio > DateTime.UtcNow.Year + 1,
            $"El anio {anio} esta fuera del rango admitido ({AnioMinimo}-{DateTime.UtcNow.Year + 1}).");

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        Anio = anio;
        Motor = string.IsNullOrWhiteSpace(motor) ? null : motor.Trim();
    }

    public override string ToString() =>
        Motor is null ? $"{Marca} {Modelo} {Anio}" : $"{Marca} {Modelo} {Anio} ({Motor})";
}
