extern alias Soap;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Clientes.Soap;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Tests.Integracion;

/// <summary>
/// El tercer camino al catalogo (Segunda Parte): Catalogo.Soap hospedado en memoria y consumido
/// con el proxy que genero dotnet-svcutil desde su WSDL. Verifica lo mismo que
/// AccesoLocalVsRemotoTests para REST: que SOAP devuelva exactamente lo que devuelve el catalogo en
/// proceso, y que sus errores lleguen como los del camino remoto.
///
/// El canal WCF usa el handler HTTP del TestServer, asi que no hacen falta puertos ni contenedores.
/// </summary>
[Collection(ColeccionBaseCompartida.Nombre)]
public class AccesoSoapTests : IClassFixture<FabricaCatalogoSoap>, IAsyncLifetime
{
    private readonly FabricaCatalogoSoap _fabrica;
    private readonly FabricaCanalSoap _canales;

    public AccesoSoapTests(FabricaCatalogoSoap fabrica)
    {
        _fabrica = fabrica;
        _canales = new FabricaCanalSoap(
            "http://localhost" + Soap::RepMatch.Catalogo.Soap.RutasSoap.Servicio,
            TimeSpan.FromSeconds(10),
            _ => fabrica.Server.CreateHandler());
    }

    /// <summary>El host siembra la base al arrancar; basta con tocarlo una vez.</summary>
    public async Task InitializeAsync() =>
        (await _fabrica.CreateClient().GetAsync("/salud")).EnsureSuccessStatusCode();

    public Task DisposeAsync()
    {
        _canales.Dispose();
        return Task.CompletedTask;
    }

    private CatalogoSoap CrearCatalogoSoap() => new(_canales, NullLogger<CatalogoSoap>.Instance);

    /// <summary>El catalogo en proceso del mismo host: misma base, mismos Id.</summary>
    private ICatalogoRepuestos CrearCatalogoLocal() =>
        _fabrica.Services.CreateScope().ServiceProvider.GetRequiredService<ICatalogoRepuestos>();

    [Fact]
    public async Task El_WSDL_publica_el_contrato_con_sus_cuatro_operaciones()
    {
        var wsdl = await _fabrica.CreateClient()
            .GetStringAsync(Soap::RepMatch.Catalogo.Soap.RutasSoap.Servicio + "?wsdl");

        Assert.Contains("targetNamespace=\"urn:repmatch:catalogo:v1\"", wsdl);
        foreach (var operacion in new[] { "ConsultarPorVehiculo", "ObtenerPorCodigo", "Listar", "VerificarCodigo" })
            Assert.Contains($"<wsdl:operation name=\"{operacion}\"", wsdl);
        Assert.DoesNotContain("tempuri.org", wsdl);
    }

    [Theory]
    [MemberData(nameof(AccesoLocalVsRemotoTests.Vehiculos), MemberType = typeof(AccesoLocalVsRemotoTests))]
    public async Task SOAP_devuelve_lo_mismo_que_el_catalogo_en_proceso(
        string marca, string modelo, int anio, string? motor)
    {
        var vehiculo = new VehiculoDto { Marca = marca, Modelo = modelo, Anio = anio, Motor = motor };

        var local = await CrearCatalogoLocal().BuscarCompatiblesAsync(vehiculo);
        var soap = await CrearCatalogoSoap().BuscarCompatiblesAsync(vehiculo);

        Assert.Equal(
            local.Select(r => r.CodigoCanonico).OrderBy(c => c),
            soap.Select(r => r.CodigoCanonico).OrderBy(c => c));
    }

    [Fact]
    public async Task El_filtro_por_sistema_viaja_por_SOAP()
    {
        var vehiculo = new VehiculoDto { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015 };

        var soap = await CrearCatalogoSoap().BuscarCompatiblesAsync(vehiculo, "Frenos");

        Assert.NotEmpty(soap);
        Assert.All(soap, r => Assert.Equal("Frenos", r.Sistema));
    }

    [Fact]
    public async Task El_listado_completo_coincide_campo_por_campo()
    {
        var local = await CrearCatalogoLocal().ListarAsync();
        var soap = await CrearCatalogoSoap().ListarAsync();

        Assert.Equal(local.Count, soap.Count);
        Assert.NotEmpty(local);
        foreach (var (esperado, obtenido) in local.Zip(soap))
        {
            Assert.Equal(esperado.Id, obtenido.Id);
            Assert.Equal(esperado.CodigoCanonico, obtenido.CodigoCanonico);
            Assert.Equal(esperado.Nombre, obtenido.Nombre);
            Assert.Equal(esperado.Descripcion, obtenido.Descripcion);
            Assert.Equal(esperado.Sistema, obtenido.Sistema);
            Assert.Equal(esperado.CodigosEquivalentes, obtenido.CodigosEquivalentes);
            Assert.Equal(esperado.Aplicaciones, obtenido.Aplicaciones);
        }
    }

    [Fact]
    public async Task Un_codigo_existente_y_uno_inexistente_se_resuelven_igual()
    {
        var soap = CrearCatalogoSoap();

        var existente = await soap.ObtenerPorCodigoAsync("past-vw-gol-del");
        Assert.NotNull(existente);
        Assert.Equal("PAST-VW-GOL-DEL", existente.CodigoCanonico);

        Assert.Null(await soap.ObtenerPorCodigoAsync("NO-EXISTE-999"));
    }

    [Fact]
    public async Task VerificarCodigo_responde_si_la_pieza_le_va_al_vehiculo()
    {
        var canal = _canales.CrearCanal();

        Assert.True(await canal.VerificarCodigoAsync("PAST-VW-GOL-DEL", "Volkswagen", "Gol", 2015, "1.6"));
        Assert.False(await canal.VerificarCodigoAsync("CORR-TOY-COR-18", "Volkswagen", "Gol", 2015, "1.6"));

        canal.Close();
    }

    [Fact]
    public void Declara_su_modo_de_acceso() => Assert.Equal("Soap", CrearCatalogoSoap().Modo);

    [Fact]
    public async Task Un_vehiculo_incompleto_vuelve_como_error_del_catalogo_remoto()
    {
        // El servicio responde un fault SOAP; el adapter lo convierte en lo que la fachada ya atrapa.
        var vehiculo = new VehiculoDto { Marca = " ", Modelo = "Gol", Anio = 2015 };

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => CrearCatalogoSoap().BuscarCompatiblesAsync(vehiculo));
        Assert.Contains("marca, modelo y año", error.Message);
    }

    [Fact]
    public async Task Con_el_servicio_caido_el_error_es_el_mismo_que_en_el_camino_REST()
    {
        // Puerto 9 (discard): nadie escucha. Sin la traduccion del adapter esto seria una
        // EndpointNotFoundException de WCF que la fachada no atrapa y tiraria la pagina.
        using var caido = new FabricaCanalSoap("http://127.0.0.1:9/ConsultaCompatibilidad.svc", TimeSpan.FromSeconds(3));
        var catalogo = new CatalogoSoap(caido, NullLogger<CatalogoSoap>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => catalogo.ListarAsync());
    }
}

/// <summary>Catalogo.Soap en memoria. El TestServer no escucha en ninguna direccion, asi que se le
/// da a CoreWCF la base con que los clientes de la prueba arman la URL del servicio.</summary>
public sealed class FabricaCatalogoSoap : WebApplicationFactory<Soap::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("Soap:DireccionBase", "http://localhost");
}
