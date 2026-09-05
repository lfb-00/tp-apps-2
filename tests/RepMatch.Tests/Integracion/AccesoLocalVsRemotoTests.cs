using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Clientes.Rest;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Repositorios;
using RepMatch.Persistence;

namespace RepMatch.Tests.Integracion;

/// <summary>
/// Version automatizada de la evidencia que pide el TP Inicial: la misma consulta, resuelta por
/// invocacion directa en proceso y por invocacion remota HTTP, tiene que devolver exactamente lo
/// mismo. Si algun dia las dos implementaciones divergen, esto falla antes que la demo.
///
/// Catalogo.Api se hospeda en memoria con WebApplicationFactory, asi que el test no necesita
/// puertos libres ni contenedores.
/// </summary>
public class AccesoLocalVsRemotoTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _fabrica;
    private readonly HttpClient _http;

    public AccesoLocalVsRemotoTests(WebApplicationFactory<Program> fabrica)
    {
        _fabrica = fabrica;
        _http = fabrica.CreateClient();
    }

    /// <summary>El host siembra la base al arrancar; basta con tocarlo una vez.</summary>
    public async Task InitializeAsync() =>
        (await _http.GetAsync("/salud")).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    private ICatalogoRepuestos CrearCatalogoLocal()
    {
        // Comparte la base InMemory con el host: EF Core la resuelve por nombre dentro del proceso.
        var alcance = _fabrica.Services.CreateScope();
        var repuestos = alcance.ServiceProvider.GetRequiredService<IRepuestoRepository>();

        return new CatalogoLocal(repuestos, NullLogger<CatalogoLocal>.Instance);
    }

    private ICatalogoRepuestos CrearCatalogoRemoto() =>
        new CatalogoRemoto(_http, NullLogger<CatalogoRemoto>.Instance);

    public static TheoryData<string, string, int, string?> Vehiculos() => new()
    {
        { "Volkswagen", "Gol", 2015, "1.6" },
        { "Volkswagen", "Gol", 2015, null },
        { "Peugeot", "208", 2019, null },
        { "Toyota", "Corolla", 2018, "1.8" },
        { "Ford", "Ka", 2016, null }   // sin repuestos catalogados: los dos deben devolver vacio
    };

    [Theory]
    [MemberData(nameof(Vehiculos))]
    public async Task Los_dos_caminos_devuelven_exactamente_lo_mismo(
        string marca, string modelo, int anio, string? motor)
    {
        var vehiculo = new VehiculoDto { Marca = marca, Modelo = modelo, Anio = anio, Motor = motor };

        var local = await CrearCatalogoLocal().BuscarCompatiblesAsync(vehiculo);
        var remoto = await CrearCatalogoRemoto().BuscarCompatiblesAsync(vehiculo);

        Assert.Equal(
            local.Select(r => r.CodigoCanonico).OrderBy(c => c),
            remoto.Select(r => r.CodigoCanonico).OrderBy(c => c));
    }

    [Fact]
    public async Task Cada_implementacion_declara_su_modo_de_acceso()
    {
        Assert.Equal("Local", CrearCatalogoLocal().Modo);
        Assert.Equal("Remoto", CrearCatalogoRemoto().Modo);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task El_listado_completo_coincide_campo_por_campo()
    {
        var local = await CrearCatalogoLocal().ListarAsync();
        var remoto = await CrearCatalogoRemoto().ListarAsync();

        Assert.Equal(local.Count, remoto.Count);
        Assert.NotEmpty(local);

        foreach (var (esperado, obtenido) in local.Zip(remoto))
        {
            Assert.Equal(esperado.Id, obtenido.Id);
            Assert.Equal(esperado.CodigoCanonico, obtenido.CodigoCanonico);
            Assert.Equal(esperado.Nombre, obtenido.Nombre);
            Assert.Equal(esperado.Descripcion, obtenido.Descripcion);
            Assert.Equal(esperado.Sistema, obtenido.Sistema);
            Assert.Equal(esperado.CodigosEquivalentes, obtenido.CodigosEquivalentes);
            Assert.Equal(esperado.Aplicaciones.Count, obtenido.Aplicaciones.Count);
        }
    }

    [Fact]
    public async Task Los_dos_caminos_resuelven_igual_un_codigo_inexistente()
    {
        Assert.Null(await CrearCatalogoLocal().ObtenerPorCodigoAsync("NO-EXISTE-999"));
        Assert.Null(await CrearCatalogoRemoto().ObtenerPorCodigoAsync("NO-EXISTE-999"));
    }

    [Fact]
    public async Task Los_dos_caminos_resuelven_igual_un_codigo_existente()
    {
        var local = await CrearCatalogoLocal().ObtenerPorCodigoAsync("past-vw-gol-del");
        var remoto = await CrearCatalogoRemoto().ObtenerPorCodigoAsync("past-vw-gol-del");

        Assert.NotNull(local);
        Assert.NotNull(remoto);
        Assert.Equal(local.CodigoCanonico, remoto.CodigoCanonico);
        Assert.Equal(local.CodigosEquivalentes, remoto.CodigosEquivalentes);
    }

    [Fact]
    public async Task El_filtro_por_sistema_se_aplica_igual_en_los_dos_caminos()
    {
        var vehiculo = new VehiculoDto { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015 };

        var local = await CrearCatalogoLocal().BuscarCompatiblesAsync(vehiculo, "Frenos");
        var remoto = await CrearCatalogoRemoto().BuscarCompatiblesAsync(vehiculo, "Frenos");

        Assert.NotEmpty(local);
        Assert.All(local, r => Assert.Equal("Frenos", r.Sistema));
        Assert.Equal(
            local.Select(r => r.CodigoCanonico).OrderBy(c => c),
            remoto.Select(r => r.CodigoCanonico).OrderBy(c => c));
    }
}
