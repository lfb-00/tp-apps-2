using RabbitMQ.Client;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Mensajeria;

/// <summary>Una cola de un consumidor y las claves de ruteo que escucha.</summary>
/// <param name="Nombre">Por ejemplo "repmatch.comisiones". Su DLQ es "repmatch.comisiones.dlq".</param>
/// <param name="ClavesRuteo">Patrones del exchange topic: "derivacion.tienda", "busqueda.*", "#".</param>
public sealed record DefinicionCola(string Nombre, IReadOnlyList<string> ClavesRuteo)
{
    public string NombreDlq => $"{Nombre}.dlq";
}

/// <summary>
/// Topologia de RepMatch en RabbitMQ (issue #12). Declarar es idempotente, asi que la declara
/// cada proceso al conectarse y nadie depende de que otro haya arrancado antes:
/// <list type="bullet">
/// <item><c>repmatch.eventos</c>: exchange topic durable donde se publica todo.</item>
/// <item>Una cola durable por consumidor, atada a las claves que le interesan. Los mensajes se
/// publican persistentes: cola durable + mensaje persistente es lo que sobrevive a un reinicio
/// del broker.</item>
/// <item><c>repmatch.eventos.dlx</c>: exchange directo de mensajes muertos. Cada cola tiene su DLQ;
/// un mensaje que se rechaza sin reencolar termina ahi en vez de dar vueltas para siempre.</item>
/// </list>
/// </summary>
public static class TopologiaRabbitMq
{
    public static string ExchangeMuertos(OpcionesMensajeria opciones) => $"{opciones.Exchange}.dlx";

    public static async Task DeclararExchangesAsync(IChannel canal, OpcionesMensajeria opciones, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(opciones.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(ExchangeMuertos(opciones), ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken: ct);
    }

    public static async Task DeclararColaAsync(
        IChannel canal, OpcionesMensajeria opciones, DefinicionCola cola, CancellationToken ct)
    {
        await DeclararExchangesAsync(canal, opciones, ct);

        await canal.QueueDeclareAsync(cola.NombreDlq, durable: true, exclusive: false, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken: ct);
        await canal.QueueBindAsync(cola.NombreDlq, ExchangeMuertos(opciones), cola.Nombre,
            arguments: null, noWait: false, cancellationToken: ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = ExchangeMuertos(opciones),
            ["x-dead-letter-routing-key"] = cola.Nombre
        };
        await canal.QueueDeclareAsync(cola.Nombre, durable: true, exclusive: false, autoDelete: false,
            arguments: argumentos, passive: false, noWait: false, cancellationToken: ct);

        foreach (var clave in cola.ClavesRuteo)
            await canal.QueueBindAsync(cola.Nombre, opciones.Exchange, clave,
                arguments: null, noWait: false, cancellationToken: ct);
    }
}
