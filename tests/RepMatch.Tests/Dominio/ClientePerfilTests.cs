using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Tests.Dominio;

/// <summary>Invariantes del perfil del cliente: foto, vehiculo predeterminado, contacto y tema.</summary>
public class ClientePerfilTests
{
    private static Cliente NuevoCliente() => new("Ana Prueba", "ana@ejemplo.com");

    [Fact]
    public void Rechaza_una_foto_vacia() =>
        Assert.Throws<ExcepcionDominio>(() => NuevoCliente().CambiarFotoPerfil([], "image/jpeg"));

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("")]
    public void Rechaza_un_tipo_de_foto_no_permitido(string tipo) =>
        Assert.Throws<ExcepcionDominio>(() => NuevoCliente().CambiarFotoPerfil([1, 2, 3], tipo));

    [Fact]
    public void Rechaza_una_foto_que_supera_el_maximo()
    {
        var demasiado = new byte[Cliente.TamanoMaximoFotoBytes + 1];

        Assert.Throws<ExcepcionDominio>(() => NuevoCliente().CambiarFotoPerfil(demasiado, "image/png"));
    }

    [Fact]
    public void Quitar_la_foto_borra_datos_y_tipo()
    {
        var cliente = NuevoCliente();
        cliente.CambiarFotoPerfil([1, 2, 3], "image/webp");

        cliente.QuitarFotoPerfil();

        Assert.Null(cliente.FotoPerfil);
        Assert.Null(cliente.FotoTipoContenido);
    }

    [Fact]
    public void Rechaza_un_vehiculo_predeterminado_ajeno()
    {
        var cliente = NuevoCliente();
        var otro = new Cliente("Otro", "otro@ejemplo.com");
        var ajeno = otro.AgregarVehiculo(new DatosVehiculo("Toyota", "Corolla", 2018));

        Assert.Throws<ExcepcionDominio>(() => cliente.EstablecerVehiculoPredeterminado(ajeno.Id));
        Assert.Null(cliente.VehiculoPredeterminadoId);
    }

    [Fact]
    public void Quitar_el_vehiculo_predeterminado_lo_limpia()
    {
        var cliente = NuevoCliente();
        var gol = cliente.AgregarVehiculo(new DatosVehiculo("Volkswagen", "Gol", 2015));
        var otro = cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019));
        cliente.EstablecerVehiculoPredeterminado(gol.Id);

        cliente.QuitarVehiculo(otro.Id);
        Assert.Equal(gol.Id, cliente.VehiculoPredeterminadoId);

        cliente.QuitarVehiculo(gol.Id);
        Assert.Null(cliente.VehiculoPredeterminadoId);
    }

    [Theory]
    [InlineData("1234567")]                 // corto
    [InlineData("+54 9 11 1234-5678 0000")] // largo
    [InlineData("11-abcd-5678")]            // letras
    [InlineData("11 1234+5678")]            // + que no esta al inicio
    public void Rechaza_un_telefono_invalido(string telefono) =>
        Assert.Throws<ExcepcionDominio>(() => NuevoCliente().ActualizarContacto(telefono, false, null, null));

    [Fact]
    public void Acepta_un_telefono_con_formato_argentino()
    {
        var cliente = NuevoCliente();

        cliente.ActualizarContacto(" +54 9 (11) 1234-5678 ", true, "Córdoba", " Villa María ");

        Assert.Equal("+54 9 (11) 1234-5678", cliente.Telefono);
        Assert.True(cliente.TieneWhatsApp);
        Assert.Equal("Villa María", cliente.Localidad);
    }

    [Fact]
    public void Sin_telefono_no_hay_whatsapp()
    {
        var cliente = NuevoCliente();

        cliente.ActualizarContacto("  ", true, null, null);

        Assert.Null(cliente.Telefono);
        Assert.False(cliente.TieneWhatsApp);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("Oscuro")]
    [InlineData("")]
    public void Rechaza_un_tema_invalido(string tema) =>
        Assert.Throws<ExcepcionDominio>(() => NuevoCliente().CambiarTema(tema));

    [Fact]
    public void Guarda_el_tema_elegido()
    {
        var cliente = NuevoCliente();

        cliente.CambiarTema(Cliente.TemaOscuro);

        Assert.Equal("oscuro", cliente.TemaPreferido);
    }
}
