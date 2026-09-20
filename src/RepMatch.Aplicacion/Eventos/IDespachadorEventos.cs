using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Publica los eventos que las entidades acumularon en <c>EntidadBase.EventosDominio</c>.
/// Lo invoca la unidad de trabajo despues de confirmar la transaccion.
///
/// En la Primera Parte la implementacion es in-process (<see cref="DespachadorEventosEnProceso"/>);
/// en la Segunda Parte se reemplaza por una que publica en RabbitMQ, sin tocar las entidades ni
/// los manejadores.
/// </summary>
public interface IDespachadorEventos
{
    Task DespacharAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default);
}
