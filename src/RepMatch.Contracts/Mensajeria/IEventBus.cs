namespace RepMatch.Contracts.Mensajeria;

/// <summary>
/// Publicacion de eventos de integracion a la cola de mensajes (Segunda Parte, issue #12). Vive en
/// Contracts para que quien publica no conozca el broker: hoy lo implementa RabbitMQ
/// (<c>RepMatch.Mensajeria</c>) y, sin broker configurado, una implementacion que no hace nada.
///
/// Un evento de integracion es un hecho que ya ocurrio y quedo guardado ("se compartio una
/// consulta"). No es un mail ni una notificacion: es lo que un consumidor recibe para decidir que
/// hacer, por ejemplo avisarle al mecanico o sumar una comision.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publica el mensaje en el exchange de eventos con su clave de ruteo
    /// (<see cref="ClavesRuteo"/>). Si el broker no responde, lanza: quien publica decide si eso
    /// corta la operacion. En RepMatch nunca la corta, porque el hecho ya esta guardado.
    /// </summary>
    Task PublicarAsync<T>(MensajeIntegracion<T> mensaje, CancellationToken ct = default);
}

/// <summary>
/// Sobre comun de todos los mensajes. El contrato es JSON versionado: un consumidor viejo ignora
/// los campos que no conoce, asi que agregar un campo no lo rompe; cambiar el significado de uno
/// existente obliga a subir <see cref="Version"/>.
/// </summary>
public sealed record MensajeIntegracion<T>
{
    /// <summary>Identifica el hecho, no el envio: si el mismo mensaje llega dos veces, el consumidor
    /// lo reconoce por este id y no lo procesa de nuevo.</summary>
    public required Guid MensajeId { get; init; }

    /// <summary>Clave de ruteo, que tambien nombra el tipo de evento ("busqueda.compartida").</summary>
    public required string Tipo { get; init; }

    public int Version { get; init; } = 1;
    public required DateTimeOffset OcurridoEn { get; init; }

    /// <summary>El de la peticion que origino el hecho: con el, un recorrido se sigue en los logs de
    /// la Web, de la cola y del consumidor con un solo id.</summary>
    public string? CorrelationId { get; init; }

    public required T Datos { get; init; }
}

/// <summary>Claves de ruteo del exchange <c>repmatch.eventos</c> (topic): entidad.hecho.</summary>
public static class ClavesRuteo
{
    public const string BusquedaCreada = "busqueda.creada";
    public const string BusquedaCompartida = "busqueda.compartida";
    public const string DiagnosticoValidado = "diagnostico.validado";
    public const string DerivacionTienda = "derivacion.tienda";
}
