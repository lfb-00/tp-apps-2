using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Un vehiculo concreto del garage de un cliente. Es entidad y no value object porque tiene
/// identidad propia: el cliente lo nombra ("el Gol de mi vieja") y busca repuestos para el.
/// </summary>
public class Vehiculo : EntidadBase
{
    private Vehiculo() { Datos = null!; }   // requerido por EF Core

    public Vehiculo(Guid clienteId, DatosVehiculo datos, string? vin = null, string? alias = null)
    {
        ExcepcionDominio.Si(clienteId == Guid.Empty, "El vehiculo debe pertenecer a un cliente.");
        ArgumentNullException.ThrowIfNull(datos);

        ClienteId = clienteId;
        Datos = datos;
        Vin = NormalizarVin(vin);
        Alias = string.IsNullOrWhiteSpace(alias) ? datos.ToString() : alias.Trim();
    }

    public Guid ClienteId { get; private set; }
    public DatosVehiculo Datos { get; private set; }

    /// <summary>VIN opcional. Cuando esta presente, la Segunda Parte lo resuelve contra la API
    /// publica de NHTSA vPIC para autocompletar marca/modelo/anio.</summary>
    public string? Vin { get; private set; }

    public string Alias { get; private set; } = string.Empty;

    public void CambiarAlias(string alias)
    {
        ExcepcionDominio.SiNulaOVacia(alias, nameof(alias));
        Alias = alias.Trim();
    }

    /// <summary>Un VIN valido tiene 17 caracteres y no usa las letras I, O ni Q.</summary>
    private static string? NormalizarVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin)) return null;

        var limpio = vin.Trim().ToUpperInvariant();
        ExcepcionDominio.Si(limpio.Length != 17, "El VIN debe tener exactamente 17 caracteres.");
        ExcepcionDominio.Si(limpio.Any(c => c is 'I' or 'O' or 'Q'),
            "El VIN no puede contener las letras I, O ni Q.");
        ExcepcionDominio.Si(!limpio.All(char.IsLetterOrDigit), "El VIN solo admite letras y digitos.");
        return limpio;
    }
}
