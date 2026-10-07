extern alias Web;

using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using RepMatch.Aplicacion.Fachada;
using Web::RepMatch.Web.Servicios;

namespace RepMatch.Tests.Presentacion;

/// <summary>
/// La sesion se guarda cifrada en el sessionStorage del navegador. Si el contenedor web se recrea
/// sin volumen de claves, la pestania conserva un valor que el proceso nuevo ya no puede
/// descifrar. Antes eso tiraba el circuito y la pagina quedaba en "Cargando...".
/// </summary>
public class SesionActualTests
{
    [Fact]
    public async Task Descarta_una_sesion_cifrada_con_claves_que_ya_no_existen()
    {
        // Cifrado por "el proceso anterior": un proveedor efimero distinto del que usa la sesion.
        var valorViejo = new EphemeralDataProtectionProvider()
            .CreateProtector("proceso anterior")
            .Protect(JsonSerializer.Serialize(Guid.NewGuid()));
        var navegador = new SessionStorageFalso(valorViejo);
        var almacenamiento = new ProtectedSessionStorage(navegador, new EphemeralDataProtectionProvider());

        // La fachada no se llega a usar: el valor guardado no se puede leer.
        var sesion = new SesionActual(
            new FachadaAplicacion(null!, null!, null!, null!), almacenamiento, NullLogger<SesionActual>.Instance);

        await sesion.RestaurarAsync();

        Assert.Null(sesion.Cliente);
        Assert.True(navegador.SeBorro);
    }

    /// <summary>Hace de sessionStorage: devuelve el valor guardado y registra si se lo borro.</summary>
    private sealed class SessionStorageFalso(string guardado) : IJSRuntime
    {
        public bool SeBorro { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier.EndsWith(".removeItem", StringComparison.Ordinal))
            {
                SeBorro = true;
                return default;
            }

            if (identifier.EndsWith(".getItem", StringComparison.Ordinal))
                return ValueTask.FromResult((TValue)(object)guardado);

            throw new NotSupportedException(identifier);
        }
    }
}
