using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>
/// Configuracion externalizada de la cola (seccion "Mensajeria"). Con <see cref="Habilitada"/> en
/// false no se publica nada y la aplicacion funciona igual: es lo que permite correr
/// <c>dotnet run</c> y <c>dotnet test</c> sin RabbitMQ. En compose se habilita por variable de
/// entorno (<c>Mensajeria__Habilitada=true</c>, <c>Mensajeria__Host=rabbitmq</c>).
/// </summary>
public sealed class OpcionesMensajeria
{
    public const string Seccion = "Mensajeria";

    public bool Habilitada { get; set; }

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Puerto { get; set; } = 5672;

    public string Usuario { get; set; } = "guest";

    /// <summary>Solo por variable de entorno o .env fuera de un laboratorio.</summary>
    public string Contrasena { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    /// <summary>Exchange de tipo topic donde se publican todos los eventos.</summary>
    public string Exchange { get; set; } = "repmatch.eventos";

    /// <summary>Espera maxima para conectar y para que el broker confirme una publicacion. Es corta a
    /// proposito: publicar ocurre dentro de una accion de la persona.</summary>
    [Range(1, 60)]
    public int TimeoutSegundos { get; set; } = 3;

    /// <summary>Despues de no poder conectar, cuanto esperar antes de intentar de nuevo. Mientras
    /// tanto las publicaciones fallan al instante, sin hacer esperar a nadie.</summary>
    [Range(1, 600)]
    public int PausaTrasFallaSegundos { get; set; } = 30;
}
