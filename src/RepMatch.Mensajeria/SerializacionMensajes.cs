using System.Text.Json;
using RepMatch.Contracts.Mensajeria;

namespace RepMatch.Mensajeria;

/// <summary>
/// JSON de los mensajes, igual para quien publica y quien consume. Los nombres van en camelCase y
/// los campos desconocidos se ignoran: es lo que permite agregar un campo sin romper a un
/// consumidor que todavia tiene la version anterior.
/// </summary>
public static class SerializacionMensajes
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static byte[] Serializar<T>(MensajeIntegracion<T> mensaje) =>
        JsonSerializer.SerializeToUtf8Bytes(mensaje, Opciones);

    /// <summary>Lanza <see cref="JsonException"/> si el cuerpo no es un sobre valido: ese mensaje no
    /// se va a poder procesar nunca y va directo a la DLQ.</summary>
    public static MensajeIntegracion<T> Deserializar<T>(ReadOnlySpan<byte> cuerpo)
    {
        var mensaje = JsonSerializer.Deserialize<MensajeIntegracion<T>>(cuerpo, Opciones)
            ?? throw new JsonException("El mensaje esta vacio.");

        if (mensaje.MensajeId == Guid.Empty || mensaje.Datos is null)
            throw new JsonException("El mensaje no tiene MensajeId o Datos.");

        return mensaje;
    }
}
