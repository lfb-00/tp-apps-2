using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Observador de un tipo concreto de evento de dominio (patron Observer). Se registra en el
/// contenedor y <see cref="IDespachadorEventos"/> lo resuelve por el tipo del evento, asi que
/// sumar un manejador nuevo no toca ni las entidades ni el despachador.
/// </summary>
/// <typeparam name="TEvento">Evento que este manejador sabe procesar.</typeparam>
public interface IManejadorEvento<in TEvento> where TEvento : IEventoDominio
{
    Task ManejarAsync(TEvento evento, CancellationToken ct = default);
}
