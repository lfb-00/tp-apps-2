using RepMatch.Aplicacion.Eventos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Persistence;

/// <summary>
/// Implementacion de <see cref="IUnitOfWork"/> sobre EF Core. El DbContext ya es una unidad de
/// trabajo: esta clase existe para que las capas superiores dependan de la abstraccion del
/// dominio y no del tipo de EF.
///
/// Ademas es el punto donde se cierra el patron Observer: una vez confirmada la transaccion,
/// recolecta los eventos que acumularon las entidades rastreadas, los entrega a
/// <see cref="IDespachadorEventos"/> y vacia los buzones. Publicar DESPUES de confirmar evita
/// notificar algo que la base termine revirtiendo.
/// </summary>
public sealed class UnitOfWork(
    RepMatchDbContext contexto,
    IDespachadorEventos despachador) : IUnitOfWork
{
    public async Task<int> ConfirmarAsync(CancellationToken ct = default)
    {
        // Se toman las entidades con eventos ANTES de guardar: una entidad borrada deja de estar
        // rastreada despues del SaveChanges y sus eventos se perderian.
        var entidades = contexto.ChangeTracker.Entries<EntidadBase>()
            .Select(e => e.Entity)
            .Where(e => e.EventosDominio.Count > 0)
            .ToList();

        var filas = await contexto.SaveChangesAsync(ct);

        if (entidades.Count == 0)
            return filas;

        // Se copian los eventos y se vacian los buzones antes de despachar: si un manejador vuelve
        // a confirmar la unidad de trabajo, no republica los mismos eventos.
        var eventos = entidades.SelectMany(e => e.EventosDominio).ToList();
        entidades.ForEach(e => e.LimpiarEventos());

        await despachador.DespacharAsync(eventos, ct);

        return filas;
    }
}
