namespace RepMatch.Domain.Comun;

/// <summary>
/// Marca un hecho relevante ocurrido en el dominio. En el TP Inicial los eventos solo se
/// acumulan; la publicacion efectiva (in-process en la Primera Parte, via RabbitMQ en la
/// Segunda) se agrega despues sin tocar las entidades.
/// </summary>
public interface IEventoDominio
{
    DateTimeOffset OcurridoEn { get; }
}
