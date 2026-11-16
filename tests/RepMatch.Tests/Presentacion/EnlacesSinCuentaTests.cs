extern alias Web;

using Microsoft.AspNetCore.DataProtection;
using RepMatch.Contracts.Dtos;
using Web::RepMatch.Web.Servicios;

namespace RepMatch.Tests.Presentacion;

/// <summary>
/// El enlace "Ir a la tienda" de quien compara sin cuenta lleva el destino firmado. Si se pudiera
/// alterar, /ir seria una redireccion abierta: cualquiera podria armar un enlace de RepMatch que
/// lleve a un sitio malicioso.
/// </summary>
public class EnlacesSinCuentaTests
{
    private static readonly OfertaDto Oferta = new()
    {
        CodigoRepuesto = "FILT-ACE-TOY-HILUX-28",
        NombreTienda = "Autodo",
        Titulo = "Filtro aceite Toyota Camry, Hilux Mann Filter",
        Precio = 11041.88m,
        Moneda = "ARS",
        PrecioTotal = 11041.88m,
        UrlOriginal = "https://www.autodo.com.ar/mann-filter-w712-83/p",
        Disponible = true,
        CapturadaEn = DateTimeOffset.UtcNow
    };

    private static EnlacesSinCuenta Crear() => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void El_enlace_lleva_a_la_tienda_de_la_oferta()
    {
        var enlaces = Crear();

        var enlace = enlaces.Crear(Oferta);

        Assert.StartsWith("/ir/", enlace, StringComparison.Ordinal);
        Assert.DoesNotContain("autodo", enlace, StringComparison.OrdinalIgnoreCase);
        var destino = enlaces.Leer(enlace["/ir/".Length..]);
        Assert.NotNull(destino);
        Assert.Equal(Oferta.UrlOriginal, destino.Url);
        Assert.Equal("Autodo", destino.Tienda);
        Assert.Equal("FILT-ACE-TOY-HILUX-28", destino.CodigoRepuesto);
    }

    [Theory]
    [InlineData("cualquier-cosa")]
    [InlineData("")]
    public void Un_enlace_que_no_firmo_la_aplicacion_no_lleva_a_ningun_lado(string token)
    {
        Assert.Null(Crear().Leer(token));
    }

    [Fact]
    public void Un_enlace_alterado_no_lleva_a_ningun_lado()
    {
        var enlaces = Crear();
        var token = enlaces.Crear(Oferta)["/ir/".Length..];
        var alterado = token[..^2] + (token[^2] == 'A' ? "BB" : "AA");

        Assert.Null(enlaces.Leer(alterado));
    }

    [Fact]
    public void Un_enlace_de_otra_instalacion_no_sirve_aca()
    {
        var token = Crear().Crear(Oferta)["/ir/".Length..];

        Assert.Null(Crear().Leer(token));
    }
}
