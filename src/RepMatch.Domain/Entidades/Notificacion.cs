using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Entidades;

public enum TipoNotificacion
{
    /// <summary>Al mecanico: un cliente le compartio una consulta para revisar.</summary>
    ConsultaCompartida = 1,

    /// <summary>Al cliente: el mecanico confirmo el diagnostico propuesto.</summary>
    DiagnosticoConfirmado = 2,

    /// <summary>Al cliente: el mecanico corrigio el diagnostico y recomendo otras piezas.</summary>
    DiagnosticoCorregido = 3
}

/// <summary>
/// Aviso para una persona sobre una de sus consultas. Lo crea el worker al consumir
/// <c>busqueda.compartida</c> y <c>diagnostico.validado</c>: quien comparte o revisa no espera a
/// que se escriba el aviso, y si el worker esta caido el evento espera en la cola y el aviso llega
/// cuando vuelve. Solo guarda ids y el tipo; el texto se arma al mostrarlo.
/// </summary>
public class Notificacion : EntidadBase
{
    private Notificacion() { }   // requerido por EF Core

    public Notificacion(Guid destinatarioId, TipoNotificacion tipo, Guid busquedaId, DateTimeOffset creadaEn)
    {
        ExcepcionDominio.Si(destinatarioId == Guid.Empty, "La notificacion no tiene destinatario.");
        ExcepcionDominio.Si(busquedaId == Guid.Empty, "La notificacion no tiene consulta.");
        ExcepcionDominio.Si(!Enum.IsDefined(tipo), "Tipo de notificacion desconocido.");

        DestinatarioId = destinatarioId;
        Tipo = tipo;
        BusquedaId = busquedaId;
        CreadaEn = creadaEn;
    }

    public Guid DestinatarioId { get; private set; }
    public TipoNotificacion Tipo { get; private set; }
    public Guid BusquedaId { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset? LeidaEn { get; private set; }

    public bool Leida => LeidaEn is not null;

    /// <summary>Idempotente: la primera lectura queda registrada.</summary>
    public void MarcarLeida(DateTimeOffset momento) => LeidaEn ??= momento;
}
