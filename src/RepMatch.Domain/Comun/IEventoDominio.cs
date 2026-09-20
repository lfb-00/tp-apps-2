namespace RepMatch.Domain.Comun;

/// <summary>
/// Marca un hecho relevante ocurrido en el dominio. Las entidades solo lo acumulan en
/// <see cref="EntidadBase.EventosDominio"/>; quien lo publica es la unidad de trabajo, despues de
/// confirmar la transaccion, a traves de un despachador declarado fuera del dominio (in-process en
/// la Primera Parte, via RabbitMQ en la Segunda). Asi las entidades no saben quien las observa.
/// </summary>
public interface IEventoDominio
{
    DateTimeOffset OcurridoEn { get; }
}
