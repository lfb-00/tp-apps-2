namespace RepMatch.Domain.Comun;

/// <summary>
/// Raiz comun de las entidades del dominio: identidad propia y buzon de eventos de dominio.
/// </summary>
public abstract class EntidadBase
{
    private readonly List<IEventoDominio> _eventos = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>Eventos acumulados y todavia no publicados.</summary>
    public IReadOnlyCollection<IEventoDominio> EventosDominio => _eventos.AsReadOnly();

    protected void RegistrarEvento(IEventoDominio evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();

    public override bool Equals(object? obj) =>
        obj is EntidadBase otra && otra.GetType() == GetType() && otra.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
