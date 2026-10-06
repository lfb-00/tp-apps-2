using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RepMatch.Aplicacion.Fachada;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Quién está usando la aplicación en este circuito. La vista se arma con este cliente,
/// no con el listado de todos.
/// </summary>
public sealed class SesionActual(FachadaAplicacion fachada, ProtectedSessionStorage almacenamiento)
{
    private const string Clave = "clienteId";
    private bool restaurada;

    public ClienteDto? Cliente { get; private set; }

    /// <summary>Mensaje para mostrar en la próxima página, por ejemplo después de borrar la
    /// cuenta. Lo consume quien lo muestra.</summary>
    public string? AvisoPendiente { get; set; }

    public event Action? Cambio;

    public async Task RestaurarAsync()
    {
        if (restaurada)
            return;

        try
        {
            var guardado = await almacenamiento.GetAsync<Guid>(Clave);
            restaurada = true;

            if (!guardado.Success || guardado.Value == Guid.Empty)
                return;

            Cliente = await fachada.ObtenerClienteAsync(guardado.Value);
        }
        catch (InvalidOperationException)
        {
            // El prerender todavía no tiene JavaScript.
        }
    }

    public async Task EntrarAsync(ClienteDto cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        Cliente = cliente;
        restaurada = true;
        await almacenamiento.SetAsync(Clave, cliente.Id);
        Cambio?.Invoke();
    }

    /// <summary>Vuelve a leer el cliente de la fachada y avisa, para que el header y las páginas
    /// muestren el perfil recién guardado sin recargar.</summary>
    public async Task RefrescarAsync()
    {
        if (Cliente is null)
            return;

        Cliente = await fachada.ObtenerClienteAsync(Cliente.Id);
        Cambio?.Invoke();
    }

    public string? TomarAviso()
    {
        var aviso = AvisoPendiente;
        AvisoPendiente = null;
        return aviso;
    }

    public async Task SalirAsync()
    {
        Cliente = null;
        await almacenamiento.DeleteAsync(Clave);
        Cambio?.Invoke();
    }
}
