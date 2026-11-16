using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Los enlaces "Ir a la tienda" de quien compara sin cuenta. Sin busqueda guardada no hay oferta
/// a la que apuntar con /derivar/{id}, asi que el enlace lleva la pieza, la tienda y el destino,
/// firmados con Data Protection y con vencimiento. Firmar es lo que impide que /ir sirva de
/// redireccion abierta: nadie puede armar un enlace de RepMatch que lleve a un sitio cualquiera.
/// El vencimiento acota cuanto vale un enlace copiado; los precios igual vencen antes.
/// </summary>
public sealed class EnlacesSinCuenta(IDataProtectionProvider proveedor)
{
    /// <summary>Un dia: alcanza para volver a la pestania mas tarde, no para guardar enlaces.</summary>
    public static readonly TimeSpan Vigencia = TimeSpan.FromDays(1);

    private readonly ITimeLimitedDataProtector _protector =
        proveedor.CreateProtector("RepMatch.Web.EnlacesSinCuenta.v1").ToTimeLimitedDataProtector();

    public sealed record Destino(string CodigoRepuesto, string Tienda, string Url);

    public string Crear(OfertaDto oferta)
    {
        ArgumentNullException.ThrowIfNull(oferta);
        var datos = JsonSerializer.Serialize(new Destino(oferta.CodigoRepuesto, oferta.NombreTienda, oferta.UrlOriginal));
        return "/ir/" + _protector.Protect(datos, Vigencia);
    }

    /// <summary>null si el enlace fue alterado, vencio o no es de esta aplicacion.</summary>
    public Destino? Leer(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<Destino>(_protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
