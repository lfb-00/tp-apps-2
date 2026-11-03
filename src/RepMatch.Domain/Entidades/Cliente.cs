using System.Text.RegularExpressions;
using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Quien usa el comparador. Es la raiz del agregado que contiene sus vehiculos.
/// Equivale al componente "Cliente" que pide la consigna del TP Inicial. Tambien representa al
/// mecanico (<see cref="Rol"/>): se descarto una entidad aparte porque duplicaba login, perfil y
/// garage, y el nombre "Cliente" se conserva por el mapeo con la consigna.
/// </summary>
public partial class Cliente : EntidadBase
{
    public const string TemaClaro = "claro";
    public const string TemaOscuro = "oscuro";

    /// <summary>Tope de la foto de perfil. La Web la achica a 256x256 antes de mandarla, asi que
    /// en la practica queda muy por debajo; el tope protege la columna de un llamador descuidado.</summary>
    public const int TamanoMaximoFotoBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> TiposFotoPermitidos = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Busquedas sin revisar que puede tener en la bandeja un mecanico del plan
    /// gratuito. Es el limite que empuja al plan Pro, que no tiene tope.</summary>
    public const int MaximoPendientesPlanGratuito = 5;

    public const int LongitudMaximaNombreTaller = 120;

    private readonly List<Vehiculo> _vehiculos = [];
    private readonly List<SistemaVehiculo> _especialidades = [];

    private Cliente() { }   // requerido por EF Core

    public Cliente(string nombre, string email, RolUsuario rol = RolUsuario.Cliente)
    {
        ExcepcionDominio.Si(!Enum.IsDefined(rol), $"El rol '{rol}' no existe.");
        ExcepcionDominio.SiNulaOVacia(nombre, nameof(nombre));
        ExcepcionDominio.SiNulaOVacia(email, nameof(email));
        ExcepcionDominio.Si(!EsEmailValido(email), $"El email '{email}' no tiene un formato valido.");

        Nombre = nombre.Trim();
        Email = email.Trim().ToLowerInvariant();
        FechaAlta = DateTimeOffset.UtcNow;
        Rol = rol;
        Plan = rol == RolUsuario.Mecanico ? PlanMecanico.Gratuito : null;
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

    public RolUsuario Rol { get; private set; }
    public bool EsMecanico => Rol == RolUsuario.Mecanico;

    /// <summary>Nombre comercial con el que el mecanico aparece en el directorio. null si no lo
    /// cargo: el directorio muestra su nombre personal.</summary>
    public string? NombreTaller { get; private set; }

    /// <summary>Sistemas en los que el mecanico se especializa. Usa la misma clasificacion que
    /// el catalogo y la IA, asi el cliente puede elegir a quien compartirle segun el problema.</summary>
    public IReadOnlyCollection<SistemaVehiculo> Especialidades => _especialidades.AsReadOnly();

    /// <summary>null para los clientes: el plan es solo del mecanico.</summary>
    public PlanMecanico? Plan { get; private set; }

    /// <summary>Tope de busquedas sin revisar en la bandeja; null es sin limite.</summary>
    public int? LimiteBusquedasPendientes =>
        Plan == PlanMecanico.Gratuito ? MaximoPendientesPlanGratuito : null;

    public void ActualizarTaller(string? nombreTaller, IEnumerable<SistemaVehiculo> especialidades)
    {
        ArgumentNullException.ThrowIfNull(especialidades);
        ExigirMecanico();

        var nombreLimpio = string.IsNullOrWhiteSpace(nombreTaller) ? null : nombreTaller.Trim();
        ExcepcionDominio.Si(nombreLimpio?.Length > LongitudMaximaNombreTaller,
            "El nombre del taller admite hasta 120 caracteres.");

        var sistemas = especialidades.Distinct().ToList();
        ExcepcionDominio.Si(sistemas.Any(s => s == SistemaVehiculo.Desconocido || !Enum.IsDefined(s)),
            "Elegí especialidades de la lista de sistemas.");

        NombreTaller = nombreLimpio;
        _especialidades.Clear();
        _especialidades.AddRange(sistemas.Order());
    }

    /// <summary>Cambia el plan sin cobrar nada: el pago lo resolveria un proveedor externo antes
    /// de llamar aca. RepMatch no procesa pagos.</summary>
    public void CambiarPlan(PlanMecanico plan)
    {
        ExigirMecanico();
        ExcepcionDominio.Si(!Enum.IsDefined(plan), $"El plan '{plan}' no existe.");
        Plan = plan;
    }

    /// <summary>Decide si entra una busqueda mas a la bandeja, dado cuantas tiene sin revisar.
    /// La cuenta la hace el servicio con el repositorio; la regla vive aca.</summary>
    public bool PuedeRecibirBusqueda(int pendientesActuales) =>
        EsMecanico && (LimiteBusquedasPendientes is not { } limite || pendientesActuales < limite);

    private void ExigirMecanico() =>
        ExcepcionDominio.Si(!EsMecanico, "Solo un mecánico tiene datos de taller y plan.");

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
