using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Mensajeria;

/// <summary>Lo que hacen los consumidores de avisos, sobre InMemory: a quien avisan, que dicen,
/// que no duplican una segunda entrega y que abrir la consulta los marca como leidos.</summary>
public class ServicioNotificacionesTests
{
    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static ServicioNotificaciones Crear(RepMatchDbContext contexto) =>
        new(new NotificacionRepository(contexto), new BusquedaRepository(contexto), new ClienteRepository(contexto),
            new UnitOfWork(contexto), NullLogger<ServicioNotificaciones>.Instance);

    /// <summary>Un cliente, un taller y una consulta compartida con el taller.</summary>
    private async Task<(Guid ClienteId, Guid MecanicoId, Guid BusquedaId)> SembrarAsync()
    {
        await using var contexto = CrearContexto();
        var cliente = new Cliente("Ana Pérez", $"ana-{Guid.NewGuid():N}@ejemplo.com");
        var mecanico = new Cliente("Carla", $"carla-{Guid.NewGuid():N}@ejemplo.com", RolUsuario.Mecanico);
        mecanico.ActualizarTaller("Motores Benítez", [SistemaVehiculo.Frenos]);
        var busqueda = new Busqueda(cliente.Id, new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
            "cuando freno vibra el volante");
        busqueda.CompartirCon(mecanico.Id);
        busqueda.LimpiarEventos();
        contexto.AddRange(cliente, mecanico, busqueda);
        await contexto.SaveChangesAsync();
        return (cliente.Id, mecanico.Id, busqueda.Id);
    }

    private static MensajeIntegracion<BusquedaCompartidaV1> Compartida(Guid cliente, Guid mecanico, Guid busqueda, Guid? id = null) => new()
    {
        MensajeId = id ?? Guid.NewGuid(),
        Tipo = ClavesRuteo.BusquedaCompartida,
        OcurridoEn = DateTimeOffset.UtcNow,
        Datos = new BusquedaCompartidaV1 { BusquedaId = busqueda, ClienteId = cliente, MecanicoId = mecanico }
    };

    private static MensajeIntegracion<DiagnosticoValidadoV1> Revisada(Guid cliente, Guid mecanico, Guid busqueda, bool confirmado) => new()
    {
        MensajeId = Guid.NewGuid(),
        Tipo = ClavesRuteo.DiagnosticoValidado,
        OcurridoEn = DateTimeOffset.UtcNow,
        Datos = new DiagnosticoValidadoV1
        {
            BusquedaId = busqueda, ClienteId = cliente, MecanicoId = mecanico, Confirmado = confirmado
        }
    };

    [Fact]
    public async Task Compartir_avisa_al_mecanico_con_el_nombre_del_cliente_y_el_auto()
    {
        var (cliente, mecanico, busqueda) = await SembrarAsync();

        await using (var contexto = CrearContexto())
            Assert.True(await Crear(contexto).AvisarCompartidaAsync(Compartida(cliente, mecanico, busqueda)));

        await using var lectura = CrearContexto();
        var bandeja = await Crear(lectura).ObtenerBandejaAsync(mecanico);
        var aviso = Assert.Single(bandeja.Recientes);
        Assert.Equal(1, bandeja.NoLeidas);
        Assert.Equal("Nueva consulta para revisar", aviso.Titulo);
        Assert.Equal("Ana Pérez te compartió su Volkswagen Gol 2015.", aviso.Detalle);
        Assert.Equal(busqueda, aviso.BusquedaId);
        Assert.Equal(0, await Crear(lectura).ContarNoLeidasAsync(cliente));
    }

    [Theory]
    [InlineData(true, "Diagnóstico confirmado", "Motores Benítez confirmó el diagnóstico de tu Volkswagen Gol 2015.")]
    [InlineData(false, "Diagnóstico corregido", "Motores Benítez recomendó otras piezas para tu Volkswagen Gol 2015.")]
    public async Task La_revision_avisa_al_cliente_si_el_mecanico_confirmo_o_corrigio(
        bool confirmado, string titulo, string detalle)
    {
        var (cliente, mecanico, busqueda) = await SembrarAsync();

        await using (var contexto = CrearContexto())
            await Crear(contexto).AvisarRevisadaAsync(Revisada(cliente, mecanico, busqueda, confirmado));

        await using var lectura = CrearContexto();
        var aviso = Assert.Single((await Crear(lectura).ObtenerBandejaAsync(cliente)).Recientes);
        Assert.Equal(titulo, aviso.Titulo);
        Assert.Equal(detalle, aviso.Detalle);
        Assert.Equal(0, await Crear(lectura).ContarNoLeidasAsync(mecanico));
    }

    [Fact]
    public async Task Una_segunda_entrega_del_mismo_mensaje_no_duplica_el_aviso()
    {
        var (cliente, mecanico, busqueda) = await SembrarAsync();
        var mensaje = Compartida(cliente, mecanico, busqueda);

        await using (var primera = CrearContexto())
            Assert.True(await Crear(primera).AvisarCompartidaAsync(mensaje));
        await using (var segunda = CrearContexto())
            Assert.False(await Crear(segunda).AvisarCompartidaAsync(mensaje));

        await using var lectura = CrearContexto();
        Assert.Equal(1, await Crear(lectura).ContarNoLeidasAsync(mecanico));
    }

    [Fact]
    public async Task Abrir_la_consulta_marca_sus_avisos_como_leidos_y_solo_los_de_esa_persona()
    {
        var (cliente, mecanico, busqueda) = await SembrarAsync();
        await using (var contexto = CrearContexto())
        {
            await Crear(contexto).AvisarCompartidaAsync(Compartida(cliente, mecanico, busqueda));
            await Crear(contexto).AvisarRevisadaAsync(Revisada(cliente, mecanico, busqueda, confirmado: true));
        }

        await using (var contexto = CrearContexto())
        {
            Assert.Equal(1, await Crear(contexto).MarcarLeidasAsync(cliente, busqueda));
            Assert.Equal(0, await Crear(contexto).MarcarLeidasAsync(cliente, busqueda));
        }

        await using var lectura = CrearContexto();
        Assert.Equal(0, await Crear(lectura).ContarNoLeidasAsync(cliente));
        Assert.Equal(1, await Crear(lectura).ContarNoLeidasAsync(mecanico));
        Assert.True((await Crear(lectura).ObtenerBandejaAsync(cliente)).Recientes.Single().Leida);
    }
}
