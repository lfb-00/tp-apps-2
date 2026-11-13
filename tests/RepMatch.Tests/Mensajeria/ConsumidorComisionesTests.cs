extern alias Worker;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Mensajeria;

/// <summary>Lo que hace el consumidor de derivacion.tienda, sobre InMemory: sumar e ignorar repetidos.</summary>
public class ConsumidorComisionesTests
{
    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static ServicioComisiones Crear(RepMatchDbContext contexto) =>
        new(new ComisionesRepository(contexto), new UnitOfWork(contexto), NullLogger<ServicioComisiones>.Instance);

    private static MensajeIntegracion<DerivacionTiendaV1> Derivacion(
        string tienda, DateTimeOffset cuando, Guid? mecanico = null, Guid? id = null) => new()
    {
        MensajeId = id ?? Guid.NewGuid(),
        Tipo = ClavesRuteo.DerivacionTienda,
        OcurridoEn = cuando,
        Datos = new DerivacionTiendaV1
        {
            DerivacionId = Guid.NewGuid(),
            BusquedaId = Guid.NewGuid(),
            OfertaId = Guid.NewGuid(),
            Tienda = tienda,
            MecanicoId = mecanico
        }
    };

    [Fact]
    public async Task Suma_por_tienda_y_por_mes_y_cuenta_las_atribuidas_a_mecanicos()
    {
        var noviembre = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
        var diciembre = new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);

        foreach (var mensaje in new[]
                 {
                     Derivacion("Easy", noviembre),
                     Derivacion("Easy", noviembre, mecanico: Guid.NewGuid()),
                     Derivacion("Frávega", noviembre),
                     Derivacion("Easy", diciembre)
                 })
        {
            await using var contexto = CrearContexto();
            Assert.True(await Crear(contexto).RegistrarDerivacionAsync(mensaje));
        }

        await using var lectura = CrearContexto();
        var filas = await Crear(lectura).ListarAsync();

        Assert.Equal(["2026-12 Easy 1", "2026-11 Easy 2", "2026-11 Frávega 1"],
            filas.Select(f => $"{f.Periodo} {f.Tienda} {f.Derivaciones}"));
        Assert.Equal(1, filas.Single(f => f is { Tienda: "Easy", Periodo: "2026-11" }).AtribuidasAMecanico);
    }

    [Fact]
    public async Task El_mismo_mensaje_entregado_dos_veces_cuenta_una_sola()
    {
        var mensaje = Derivacion("Easy", DateTimeOffset.UtcNow);

        await using (var contexto = CrearContexto())
            Assert.True(await Crear(contexto).RegistrarDerivacionAsync(mensaje));
        await using (var contexto = CrearContexto())
            Assert.False(await Crear(contexto).RegistrarDerivacionAsync(mensaje));

        await using var lectura = CrearContexto();
        Assert.Equal(1, Assert.Single(await Crear(lectura).ListarAsync()).Derivaciones);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("202611")]
    [InlineData("")]
    public void El_periodo_es_un_mes_valido(string periodo)
    {
        Assert.Throws<ExcepcionDominio>(() => new ComisionesTienda("Easy", periodo));
    }

    [Fact]
    public async Task El_worker_responde_salud_y_el_registro_aunque_no_haya_broker()
    {
        await using var fabrica = new WebApplicationFactory<Worker::Program>()
            .WithWebHostBuilder(b => b.UseSetting("Mensajeria:Habilitada", "false"));
        var http = fabrica.CreateClient();

        var salud = await http.GetAsync("/salud");
        var comisiones = await http.GetFromJsonAsync<List<ComisionesTiendaDto>>("/api/comisiones");

        salud.EnsureSuccessStatusCode();
        Assert.Contains("mensajeria desactivada", await salud.Content.ReadAsStringAsync());
        Assert.NotNull(comisiones);
    }
}
