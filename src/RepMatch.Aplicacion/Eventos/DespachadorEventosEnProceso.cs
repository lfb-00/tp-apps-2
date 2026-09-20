using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Despachador in-process: por cada evento resuelve del contenedor todos los
/// <see cref="IManejadorEvento{TEvento}"/> registrados para su tipo concreto y los invoca en orden.
///
/// Un manejador que falla se registra en el log y NO interrumpe a los demas ni al llamador: cuando
/// se despacha, la transaccion ya esta confirmada y no hay nada que revertir.
/// </summary>
public sealed class DespachadorEventosEnProceso(
    IServiceProvider servicios,
    ILogger<DespachadorEventosEnProceso> log) : IDespachadorEventos
{
    // El tipo concreto del evento solo se conoce en runtime; se construye una sola vez por tipo el
    // invocador generico que sabe pedir IManejadorEvento<TEvento> al contenedor.
    private static readonly ConcurrentDictionary<Type, Invocador> Invocadores = new();

    public async Task DespacharAsync(
        IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        foreach (var evento in eventos)
        {
            var invocador = Invocadores.GetOrAdd(evento.GetType(), static tipo =>
                (Invocador)Activator.CreateInstance(typeof(Invocador<>).MakeGenericType(tipo))!);

            await invocador.InvocarAsync(servicios, evento, log, ct);
        }
    }

    private abstract class Invocador
    {
        public abstract Task InvocarAsync(
            IServiceProvider servicios, IEventoDominio evento, ILogger log, CancellationToken ct);
    }

    private sealed class Invocador<TEvento> : Invocador where TEvento : IEventoDominio
    {
        public override async Task InvocarAsync(
            IServiceProvider servicios, IEventoDominio evento, ILogger log, CancellationToken ct)
        {
            var manejadores = servicios.GetServices<IManejadorEvento<TEvento>>().ToList();

            log.LogDebug("Despachando {Evento} a {Cantidad} manejador(es)",
                typeof(TEvento).Name, manejadores.Count);

            foreach (var manejador in manejadores)
            {
                try
                {
                    await manejador.ManejarAsync((TEvento)evento, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;   // cancelo el llamador, no fallo el manejador
                }
                catch (Exception ex)
                {
                    log.LogError(ex,
                        "El manejador {Manejador} fallo procesando {Evento}. " +
                        "La transaccion ya esta confirmada; se continua con el resto.",
                        manejador.GetType().Name, typeof(TEvento).Name);
                }
            }
        }
    }
}
