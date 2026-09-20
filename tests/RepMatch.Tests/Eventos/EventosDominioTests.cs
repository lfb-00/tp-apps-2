using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Eventos.Manejadores;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.Repositorios;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Eventos;

/// <summary>Observador de prueba: guarda todo lo que recibe para poder afirmar sobre ello.</summary>
internal sealed class ManejadorEspia : IManejadorEvento<BusquedaCreada>
{
    public List<BusquedaCreada> Recibidos { get; } = [];

    public Task ManejarAsync(BusquedaCreada evento, CancellationToken ct = default)
    {
        Recibidos.Add(evento);
        return Task.CompletedTask;
    }
}

/// <summary>Observador que siempre falla, para probar que no arrastra a nadie.</summary>
internal sealed class ManejadorQueFalla : IManejadorEvento<BusquedaCreada>
{
    public Task ManejarAsync(BusquedaCreada evento, CancellationToken ct = default) =>
        throw new InvalidOperationException("Este manejador siempre falla.");
}

/// <summary>
/// El despachador in-process, aislado: resuelve los manejadores por tipo de evento y aguanta que
/// uno falle sin dejar de notificar a los demas.
/// </summary>
public class DespachadorEventosTests
{
    private static BusquedaCreada NuevoEvento() =>
        new(Guid.NewGuid(), Guid.NewGuid(),
            new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"), "cuando freno vibra el volante");

    private static IDespachadorEventos CrearDespachador(
        params IManejadorEvento<BusquedaCreada>[] manejadores)
    {
        var servicios = new ServiceCollection();
        foreach (var manejador in manejadores)
            servicios.AddSingleton(manejador);

        return new DespachadorEventosEnProceso(
            servicios.BuildServiceProvider(), NullLogger<DespachadorEventosEnProceso>.Instance);
    }

    [Fact]
    public async Task Entrega_el_evento_a_todos_los_manejadores_registrados()
    {
        var uno = new ManejadorEspia();
        var otro = new ManejadorEspia();
        var evento = NuevoEvento();

        await CrearDespachador(uno, otro).DespacharAsync([evento]);

        Assert.Same(evento, Assert.Single(uno.Recibidos));
        Assert.Same(evento, Assert.Single(otro.Recibidos));
    }

    [Fact]
    public async Task Un_manejador_que_falla_no_impide_que_los_demas_reciban_el_evento()
    {
        var espia = new ManejadorEspia();
        var despachador = CrearDespachador(new ManejadorQueFalla(), espia);

        var excepcion = await Record.ExceptionAsync(() => despachador.DespacharAsync([NuevoEvento()]));

        Assert.Null(excepcion);
        Assert.Single(espia.Recibidos);
    }

    [Fact]
    public async Task Un_evento_sin_observadores_se_ignora_sin_error()
    {
        var espia = new ManejadorEspia();
        var despachador = CrearDespachador(espia);

        var excepcion = await Record.ExceptionAsync(() =>
            despachador.DespacharAsync([new EventoSinObservadores(), NuevoEvento()]));

        Assert.Null(excepcion);
        Assert.Single(espia.Recibidos);
    }

    private sealed record EventoSinObservadores : IEventoDominio
    {
        public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// El cierre del patron Observer: la unidad de trabajo publica los eventos acumulados recien
/// despues de confirmar, una sola vez, y deja los buzones vacios. Corre sobre EF Core InMemory
/// con el despachador y el manejador de log reales registrados por AgregarAplicacion.
/// </summary>
public class UnitOfWorkEventosTests
{
    private static Busqueda NuevaBusqueda() =>
        new(Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
            "cuando freno vibra el volante");

    private static ServiceProvider CrearContenedor(
        ManejadorEspia espia, bool conManejadorQueFalla = false, IInterceptor? interceptor = null)
    {
        // El nombre se fija afuera del lambda: las opciones se construyen por alcance y cada
        // alcance tiene que ver la misma base.
        var nombreBase = Guid.NewGuid().ToString();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<RepMatchDbContext>(opciones =>
        {
            opciones.UseInMemoryDatabase(nombreBase);
            if (interceptor is not null)
                opciones.AddInterceptors(interceptor);
        });

        servicios.AddScoped<IClienteRepository, ClienteRepository>();
        servicios.AddScoped<IRepuestoRepository, RepuestoRepository>();
        servicios.AddScoped<IBusquedaRepository, BusquedaRepository>();
        servicios.AddScoped<IUnitOfWork, UnitOfWork>();
        servicios.AddScoped<ICatalogoRepuestos, CatalogoLocal>();
        servicios.AgregarAplicacion();

        // Orden deliberado: el que falla va ANTES del espia, para probar que se sigue con el resto.
        if (conManejadorQueFalla)
            servicios.AddScoped<IManejadorEvento<BusquedaCreada>, ManejadorQueFalla>();

        servicios.AddSingleton<IManejadorEvento<BusquedaCreada>>(espia);

        return servicios.BuildServiceProvider();
    }

    [Fact]
    public async Task Publica_BusquedaCreada_al_confirmar_y_deja_el_buzon_vacio()
    {
        var espia = new ManejadorEspia();
        await using var contenedor = CrearContenedor(espia);
        using var alcance = contenedor.CreateScope();
        var busqueda = NuevaBusqueda();

        await alcance.ServiceProvider.GetRequiredService<IBusquedaRepository>().AgregarAsync(busqueda);
        Assert.Empty(espia.Recibidos);   // todavia no se confirmo: nadie tiene que enterarse

        await alcance.ServiceProvider.GetRequiredService<IUnitOfWork>().ConfirmarAsync();

        var evento = Assert.Single(espia.Recibidos);
        Assert.Equal(busqueda.Id, evento.BusquedaId);
        Assert.Equal(busqueda.ClienteId, evento.ClienteId);
        Assert.Empty(busqueda.EventosDominio);
    }

    [Fact]
    public async Task Publica_una_sola_vez_aunque_se_confirme_varias_veces()
    {
        var espia = new ManejadorEspia();
        await using var contenedor = CrearContenedor(espia);
        using var alcance = contenedor.CreateScope();
        var unidad = alcance.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var busqueda = NuevaBusqueda();

        await alcance.ServiceProvider.GetRequiredService<IBusquedaRepository>().AgregarAsync(busqueda);
        await unidad.ConfirmarAsync();

        busqueda.AsignarCodigosObjetivo(["PAST-VW-GOL-DEL"]);
        await unidad.ConfirmarAsync();
        await unidad.ConfirmarAsync();

        Assert.Single(espia.Recibidos);
    }

    [Fact]
    public async Task Un_manejador_fallido_no_rompe_la_transaccion_ya_confirmada()
    {
        var espia = new ManejadorEspia();
        await using var contenedor = CrearContenedor(espia, conManejadorQueFalla: true);
        var busqueda = NuevaBusqueda();

        using (var alcance = contenedor.CreateScope())
        {
            await alcance.ServiceProvider.GetRequiredService<IBusquedaRepository>().AgregarAsync(busqueda);

            var filas = await alcance.ServiceProvider.GetRequiredService<IUnitOfWork>().ConfirmarAsync();

            Assert.True(filas > 0);
        }

        // La busqueda quedo persistida y los demas observadores fueron notificados igual.
        using (var alcance = contenedor.CreateScope())
        {
            var leida = await alcance.ServiceProvider.GetRequiredService<IBusquedaRepository>()
                .ObtenerPorIdAsync(busqueda.Id);

            Assert.NotNull(leida);
        }

        Assert.Single(espia.Recibidos);
    }

    [Fact]
    public async Task No_publica_nada_si_la_persistencia_falla()
    {
        var espia = new ManejadorEspia();
        await using var contenedor = CrearContenedor(espia, interceptor: new InterceptorBaseCaida());
        using var alcance = contenedor.CreateScope();
        var busqueda = NuevaBusqueda();

        await alcance.ServiceProvider.GetRequiredService<IBusquedaRepository>().AgregarAsync(busqueda);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            alcance.ServiceProvider.GetRequiredService<IUnitOfWork>().ConfirmarAsync());

        Assert.Empty(espia.Recibidos);
        Assert.Single(busqueda.EventosDominio);   // el buzon sigue intacto para un reintento
    }

    [Fact]
    public async Task Crear_una_busqueda_desde_el_servicio_llega_al_manejador()
    {
        var espia = new ManejadorEspia();
        await using var contenedor = CrearContenedor(espia);
        using var alcance = contenedor.CreateScope();
        var servicios = alcance.ServiceProvider;

        await DatosSemilla.SembrarAsync(servicios.GetRequiredService<RepMatchDbContext>());
        var cliente = await servicios.GetRequiredService<IClienteRepository>()
            .ObtenerPorEmailAsync("blofaro@uade.edu.ar");
        Assert.NotNull(cliente);

        var resultado = await servicios.GetRequiredService<ServicioBusquedas>().CrearAsync(new CrearBusquedaDto
        {
            ClienteId = cliente.Id,
            Vehiculo = new VehiculoDto { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015, Motor = "1.6" },
            TextoLibre = "cuando freno vibra el volante"
        });

        Assert.True(resultado.EsExitoso, resultado.Error);

        var evento = Assert.Single(espia.Recibidos);
        Assert.Equal(resultado.Valor.Id, evento.BusquedaId);
        Assert.Equal(cliente.Id, evento.ClienteId);
        Assert.Equal("cuando freno vibra el volante", evento.TextoLibre);
    }

    [Fact]
    public void La_composicion_de_los_hosts_resuelve_el_despachador_y_el_manejador_de_ejemplo()
    {
        // Mismo registro que usan Web y Catalogo.Api, con el proveedor InMemory por defecto.
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AgregarPersistencia(new ConfigurationBuilder().Build());
        servicios.AgregarAplicacion();

        using var contenedor = servicios.BuildServiceProvider();
        using var alcance = contenedor.CreateScope();

        Assert.IsType<UnitOfWork>(alcance.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<DespachadorEventosEnProceso>(
            alcance.ServiceProvider.GetRequiredService<IDespachadorEventos>());
        Assert.Contains(
            alcance.ServiceProvider.GetServices<IManejadorEvento<BusquedaCreada>>(),
            manejador => manejador is ManejadorLogBusquedaCreada);
    }

    /// <summary>Simula una base caida: SaveChanges lanza antes de escribir nada.</summary>
    private sealed class InterceptorBaseCaida : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("La base no esta disponible.");
    }
}

/// <summary>
/// Criterio de aceptacion del issue: el dominio sigue sin referencias a infraestructura. Se
/// verifica sobre el ensamblado compilado, no leyendo el csproj.
/// </summary>
public class IndependenciaDelDominioTests
{
    [Fact]
    public void El_dominio_no_referencia_infraestructura()
    {
        var referencias = typeof(EntidadBase).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        Assert.NotEmpty(referencias);
        Assert.All(referencias, nombre => Assert.StartsWith("System.", nombre));
    }
}
