namespace RepMatch.Web.Servicios;

/// <summary>
/// Aviso dentro de la pestania (un circuito de Blazor) de que cambiaron las notificaciones, por
/// ejemplo porque la pantalla de una consulta las marco como leidas. La campana lo escucha para
/// actualizar el numero en el momento, sin esperar a su proxima consulta periodica.
/// </summary>
public sealed class AvisosNotificaciones
{
    public event Action? Cambio;

    public void Avisar() => Cambio?.Invoke();
}
