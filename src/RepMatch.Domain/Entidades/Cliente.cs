using System.Text.RegularExpressions;
using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Quien usa el comparador. Es la raiz del agregado que contiene sus vehiculos.
/// Equivale al componente "Cliente" que pide la consigna del TP Inicial.
/// </summary>
public partial class Cliente : EntidadBase
{
    public const string TemaClaro = "claro";
    public const string TemaOscuro = "oscuro";

    /// <summary>Tope de la foto de perfil. La Web la achica a 256x256 antes de mandarla, asi que
    /// en la practica queda muy por debajo; el tope protege la columna de un llamador descuidado.</summary>
    public const int TamanoMaximoFotoBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> TiposFotoPermitidos = ["image/jpeg", "image/png", "image/webp"];

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
    public string? HashContrasena { get; private set; }
    public DateTimeOffset FechaAlta { get; private set; }

    public string? Telefono { get; private set; }
    public bool TieneWhatsApp { get; private set; }

    /// <summary>Nombre de la jurisdiccion tal como se muestra. Se guarda como texto y no como
    /// enumeracion: es un dato de contacto, no algo sobre lo que el dominio decida.</summary>
    public string? Provincia { get; private set; }
    public string? Localidad { get; private set; }

    public byte[]? FotoPerfil { get; private set; }
    public string? FotoTipoContenido { get; private set; }

    public Guid? VehiculoPredeterminadoId { get; private set; }

    /// <summary>"claro", "oscuro" o null si nunca eligio: en ese caso manda el navegador.</summary>
    public string? TemaPreferido { get; private set; }

    public IReadOnlyCollection<Vehiculo> Vehiculos => _vehiculos.AsReadOnly();

    public void CambiarNombre(string nombre)
    {
        ExcepcionDominio.SiNulaOVacia(nombre, nameof(nombre));
        ExcepcionDominio.Si(nombre.Trim().Length < 2, "El nombre debe tener al menos 2 caracteres.");
        Nombre = nombre.Trim();
    }

    public void ActualizarContacto(string? telefono, bool tieneWhatsApp, string? provincia, string? localidad)
    {
        var telefonoLimpio = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        ExcepcionDominio.Si(telefonoLimpio is not null && !TelefonoValido().IsMatch(telefonoLimpio),
            "El telefono solo admite digitos, espacios, guiones, parentesis y un + inicial, entre 8 y 20 caracteres.");

        var localidadLimpia = string.IsNullOrWhiteSpace(localidad) ? null : localidad.Trim();
        ExcepcionDominio.Si(localidadLimpia?.Length > 100, "La localidad admite hasta 100 caracteres.");

        Telefono = telefonoLimpio;
        TieneWhatsApp = telefonoLimpio is not null && tieneWhatsApp;
        Provincia = string.IsNullOrWhiteSpace(provincia) ? null : provincia.Trim();
        Localidad = localidadLimpia;
    }

    public void CambiarFotoPerfil(byte[] datos, string tipoContenido)
    {
        ExcepcionDominio.Si(datos is null || datos.Length == 0, "La foto de perfil esta vacia.");
        ExcepcionDominio.Si(datos!.Length > TamanoMaximoFotoBytes, "La foto de perfil supera el maximo de 5 MB.");
        ExcepcionDominio.Si(!TiposFotoPermitidos.Contains(tipoContenido),
            "Formato de foto no permitido. Usa JPG, PNG o WEBP.");

        FotoPerfil = datos;
        FotoTipoContenido = tipoContenido;
    }

    public void QuitarFotoPerfil()
    {
        FotoPerfil = null;
        FotoTipoContenido = null;
    }

    /// <summary>null deja al cliente sin predeterminado.</summary>
    public void EstablecerVehiculoPredeterminado(Guid? vehiculoId)
    {
        ExcepcionDominio.Si(vehiculoId is { } id && _vehiculos.All(v => v.Id != id),
            "El vehiculo no pertenece a este cliente.");
        VehiculoPredeterminadoId = vehiculoId;
    }

    public void CambiarTema(string tema)
    {
        ExcepcionDominio.Si(tema is not (TemaClaro or TemaOscuro),
            $"El tema debe ser '{TemaClaro}' u '{TemaOscuro}'.");
        TemaPreferido = tema;
    }

    public void EstablecerContrasena(string hash)
    {
        ExcepcionDominio.SiNulaOVacia(hash, nameof(hash));
        HashContrasena = hash;
    }

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

        if (VehiculoPredeterminadoId == vehiculoId)
            VehiculoPredeterminadoId = null;
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

    [GeneratedRegex(@"^(?=.{8,20}$)\+?[0-9 ()-]+$")]
    private static partial Regex TelefonoValido();
}
