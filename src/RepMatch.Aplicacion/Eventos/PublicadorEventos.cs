using Microsoft.Extensions.Logging;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Productor de mensajes (issue #13): un observador mas de los eventos de dominio, que los traduce
/// a eventos de integracion y los publica en la cola. Asi el despacho queda compuesto sin tocar
/// <c>ServicioBusquedas</c>: primero los observadores en proceso (ObservadorCompatibilidad llena los
/// codigos que la pantalla necesita ya) y despues la publicacion. Como el despachador corre despues
/// de confirmar la transaccion, nunca se publica un hecho que no quedo guardado.
///
/// Si el broker no responde, la operacion de la persona sigue igual: el hecho ya esta en la base y
/// el problema queda en el log. El mensaje de ese momento se pierde; garantizar la entrega
/// necesitaria un outbox, que se dejo fuera de alcance.
/// </summary>
public sealed class PublicadorEventos(IEventBus bus, ILogger<PublicadorEventos> log) : IObservadorEventoDominio
{
    public bool PuedeObservar(IEventoDominio evento) =>
        evento is BusquedaCreada or BusquedaCompartida or DiagnosticoValidado or DerivacionATienda;

    public async Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default)
    {
        try
        {
            switch (evento)
            {
                case BusquedaCreada e:
                    await PublicarAsync(ClavesRuteo.BusquedaCreada, e, new BusquedaCreadaV1
                    {
                        BusquedaId = e.BusquedaId,
                        ClienteId = e.ClienteId,
                        Marca = e.Vehiculo.Marca,
                        Modelo = e.Vehiculo.Modelo,
                        Anio = e.Vehiculo.Anio,
                        Motor = e.Vehiculo.Motor
                    }, ct);
                    break;
                case BusquedaCompartida e:
                    await PublicarAsync(ClavesRuteo.BusquedaCompartida, e, new BusquedaCompartidaV1
                    {
                        BusquedaId = e.BusquedaId,
                        ClienteId = e.ClienteId,
                        MecanicoId = e.MecanicoId
                    }, ct);
                    break;
                case DiagnosticoValidado e:
                    await PublicarAsync(ClavesRuteo.DiagnosticoValidado, e, new DiagnosticoValidadoV1
                    {
                        BusquedaId = e.BusquedaId,
                        ClienteId = e.ClienteId,
                        MecanicoId = e.MecanicoId,
                        Confirmado = e.Confirmado,
                        CodigosPropuestos = e.CodigosPropuestos,
                        CodigosRecomendados = e.CodigosRecomendados
                    }, ct);
                    break;
                case DerivacionATienda e:
                    await PublicarAsync(ClavesRuteo.DerivacionTienda, e, new DerivacionTiendaV1
                    {
                        DerivacionId = e.DerivacionId,
                        BusquedaId = e.BusquedaId,
                        OfertaId = e.OfertaId,
                        Tienda = e.NombreTienda,
                        MecanicoId = e.MecanicoId
                    }, ct);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex,
                "No se pudo publicar {Evento} en la cola; la operacion siguio igual", evento.GetType().Name);
        }
    }

    private Task PublicarAsync<T>(string clave, IEventoDominio evento, T datos, CancellationToken ct) =>
        bus.PublicarAsync(new MensajeIntegracion<T>
        {
            MensajeId = Guid.NewGuid(),
            Tipo = clave,
            OcurridoEn = evento.OcurridoEn,
            CorrelationId = ContextoCorrelacion.Actual,
            Datos = datos
        }, ct);
}

/// <summary>
/// El bus cuando no hay broker configurado (<c>Mensajeria:Habilitada=false</c>, el valor por
/// defecto fuera de Docker): no publica nada. Permite correr la aplicacion y las pruebas sin
/// RabbitMQ; el host lo reemplaza por el real con <c>AgregarMensajeria</c>.
/// </summary>
public sealed class BusDesactivado(ILogger<BusDesactivado> log) : IEventBus
{
    public Task PublicarAsync<T>(MensajeIntegracion<T> mensaje, CancellationToken ct = default)
    {
        log.LogDebug("Mensajeria desactivada: {Tipo} no se publica", mensaje.Tipo);
        return Task.CompletedTask;
    }
}
