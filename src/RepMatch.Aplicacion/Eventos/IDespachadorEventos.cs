using RepMatch.Domain.Comun;

namespace RepMatch.Aplicacion.Eventos;

/// <summary>
/// Publica eventos de dominio a los observadores registrados en el proceso.
/// </summary>
public interface IDespachadorEventos
{
    Task DespacharAsync(IEnumerable<IEventoDominio> eventos, CancellationToken ct = default);
}
