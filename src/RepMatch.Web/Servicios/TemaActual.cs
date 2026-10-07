using Microsoft.JSInterop;
using RepMatch.Aplicacion.Fachada;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Tema claro u oscuro de este circuito. Sin sesión vive solo en localStorage; con sesión
/// también se guarda en el cliente, y al entrar manda la preferencia guardada.
/// </summary>
public sealed class TemaActual(IJSRuntime js, SesionActual sesion, FachadaAplicacion fachada)
{
    public const string Claro = "claro";
    public const string Oscuro = "oscuro";

    private IJSObjectReference? modulo;

    public string Tema { get; private set; } = Claro;
    public bool EsOscuro => Tema == Oscuro;

    public event Action? Cambio;

    /// <summary>Lee el tema que ya aplicó el script del head. Requiere JS: llamar después del
    /// primer render.</summary>
    public async Task InicializarAsync()
    {
        modulo ??= await js.InvokeAsync<IJSObjectReference>("import", "./tema.js");
        Tema = await modulo.InvokeAsync<string>("obtener");
        Cambio?.Invoke();
    }

    /// <summary>Si la persona con sesión tiene un tema guardado, lo aplica y lo copia a
    /// localStorage.</summary>
    public async Task SincronizarConSesionAsync()
    {
        var preferido = sesion.Cliente?.TemaPreferido;
        if (preferido is null || preferido == Tema)
            return;

        await AplicarAsync(preferido);
    }

    public async Task CambiarAsync(string tema)
    {
        await AplicarAsync(tema);

        if (sesion.Cliente is not null && sesion.Cliente.TemaPreferido != tema)
        {
            var resultado = await fachada.CambiarTemaAsync(sesion.Cliente.Id, tema);
            if (resultado.EsExitoso)
                await sesion.RefrescarAsync();
        }
    }

    public Task AlternarAsync() => CambiarAsync(EsOscuro ? Claro : Oscuro);

    private async Task AplicarAsync(string tema)
    {
        modulo ??= await js.InvokeAsync<IJSObjectReference>("import", "./tema.js");
        await modulo.InvokeVoidAsync("aplicar", tema);
        Tema = tema;
        Cambio?.Invoke();
    }
}
