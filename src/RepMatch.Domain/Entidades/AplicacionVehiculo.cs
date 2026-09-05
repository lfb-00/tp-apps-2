using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Regla de compatibilidad: "este repuesto entra en tal marca/modelo entre tal y tal anio".
/// Es la tabla que consulta el servicio SOAP ConsultaCompatibilidad de la Segunda Parte.
/// </summary>
public class AplicacionVehiculo : EntidadBase
{
    private AplicacionVehiculo() { }   // requerido por EF Core

    public AplicacionVehiculo(string marca, string modelo, int anioDesde, int anioHasta, string? motor = null)
    {
        ExcepcionDominio.SiNulaOVacia(marca, nameof(marca));
        ExcepcionDominio.SiNulaOVacia(modelo, nameof(modelo));
        ExcepcionDominio.Si(anioDesde > anioHasta,
            $"El rango de anios es invalido: {anioDesde} > {anioHasta}.");

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        AnioDesde = anioDesde;
        AnioHasta = anioHasta;
        Motor = string.IsNullOrWhiteSpace(motor) ? null : motor.Trim();
    }

    public Guid RepuestoId { get; private set; }
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public int AnioDesde { get; private set; }
    public int AnioHasta { get; private set; }

    /// <summary>Motor especifico. Si es null la aplicacion vale para todas las motorizaciones.</summary>
    public string? Motor { get; private set; }

    public bool EsCompatibleCon(DatosVehiculo vehiculo)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var coincideMarca = Marca.Equals(vehiculo.Marca, StringComparison.OrdinalIgnoreCase);
        var coincideModelo = Modelo.Equals(vehiculo.Modelo, StringComparison.OrdinalIgnoreCase);
        var dentroDelRango = vehiculo.Anio >= AnioDesde && vehiculo.Anio <= AnioHasta;

        // Motor null en la aplicacion = comodin; motor null en el vehiculo = no descarta.
        var coincideMotor = Motor is null
            || vehiculo.Motor is null
            || Motor.Equals(vehiculo.Motor, StringComparison.OrdinalIgnoreCase);

        return coincideMarca && coincideModelo && dentroDelRango && coincideMotor;
    }

    public override string ToString() =>
        $"{Marca} {Modelo} {AnioDesde}-{AnioHasta}{(Motor is null ? "" : $" [{Motor}]")}";
}
