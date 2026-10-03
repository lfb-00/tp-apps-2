namespace RepMatch.Domain.Comun;

/// <summary>
/// Hecho relevante del dominio. La entidad lo acumula y el despachador lo publica a los
/// observadores del proceso.
/// </summary>
public interface IEventoDominio
{
    DateTimeOffset OcurridoEn { get; }
}
