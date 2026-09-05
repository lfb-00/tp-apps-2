using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Quien usa el comparador. Es la raiz del agregado que contiene sus vehiculos.
/// Equivale al componente "Cliente" que pide la consigna del TP Inicial.
/// </summary>
public class Cliente : EntidadBase
{
    private readonly List<Vehiculo> _vehiculos = [];

    private Cliente() { }   // requerido por EF Core

    public Cliente(string nombre, string email)
    {
        ExcepcionDominio.SiNulaOVacia(nombre, nameof(nombre));
        ExcepcionDominio.SiNulaOVacia(email, nameof(email));
        ExcepcionDominio.Si(!EsEmailValido(email), $"El email '{email}' no tiene un formato valido.");

        Nombre = nombre.Trim();
        Email = email.Trim().ToLowerInvariant();
        FechaAlta = DateTimeOffset.UtcNow;
    }

    public string Nombre { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset FechaAlta { get; private set; }

    public IReadOnlyCollection<Vehiculo> Vehiculos => _vehiculos.AsReadOnly();

    public Vehiculo AgregarVehiculo(DatosVehiculo datos, string? vin = null, string? alias = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExcepcionDominio.Si(_vehiculos.Any(v => v.Datos == datos),
            $"El cliente ya tiene registrado un {datos}.");

        var vehiculo = new Vehiculo(Id, datos, vin, alias);
        _vehiculos.Add(vehiculo);
        return vehiculo;
    }

    public void QuitarVehiculo(Guid vehiculoId)
    {
        var vehiculo = _vehiculos.SingleOrDefault(v => v.Id == vehiculoId)
            ?? throw new ExcepcionDominio("El vehiculo no pertenece a este cliente.");
        _vehiculos.Remove(vehiculo);
    }

    /// <summary>Validacion deliberadamente minima: el formato estricto vive en el componente de
    /// utilidad (FluentValidation). Aca solo se protege la invariante.</summary>
    private static bool EsEmailValido(string email)
    {
        // Se recorta primero: el constructor tambien recorta, y rechazar
        // "ana@ejemplo.com " por un espacio al final seria un mensaje de error incomprensible.
        var limpio = email.Trim();
        var partes = limpio.Split('@');

        return partes.Length == 2
            && partes.All(p => p.Length > 0)
            && partes[1].Contains('.')
            && !limpio.Contains(' ');
    }
}
