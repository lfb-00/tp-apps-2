using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Common.Logging;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.ValueObjects;
using RepMatch.Mensajeria;

namespace RepMatch.Tests.Mensajeria;

/// <summary>El productor (issue #13): que publica, con que clave, y que no rompe nada si el broker no esta.</summary>
public class PublicadorEventosTests
{
    /// <summary>Guarda lo publicado, o falla como un broker caido.</summary>
    private sealed class BusFalso(bool caido = false) : IEventBus
    {
        public List<(string Tipo, object Datos, string? Correlacion)> Publicados { get; } = [];

        public Task PublicarAsync<T>(MensajeIntegracion<T> mensaje, CancellationToken ct = default)
        {
            if (caido)
                throw new BrokerNoDisponibleException("RabbitMQ no responde.");

            Publicados.Add((mensaje.Tipo, mensaje.Datos!, mensaje.CorrelationId));
            return Task.CompletedTask;
        }
    }

    private static PublicadorEventos Crear(IEventBus bus) => new(bus, NullLogger<PublicadorEventos>.Instance);

    [Fact]
    public async Task Publica_cada_evento_con_su_clave_de_ruteo()
    {
        var bus = new BusFalso();
        var publicador = Crear(bus);
        IEventoDominio[] eventos =
        [
            new BusquedaCreada(Guid.NewGuid(), Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"), "no arranca"),
            new BusquedaCompartida(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new DiagnosticoValidado(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, ["A"], ["B"]),
            new DerivacionATienda(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Easy", null)
        ];

        foreach (var evento in eventos)
        {
            Assert.True(publicador.PuedeObservar(evento));
            await publicador.ObservarAsync(evento);
        }

        Assert.Equal(
            ["busqueda.creada", "busqueda.compartida", "diagnostico.validado", "derivacion.tienda"],
            bus.Publicados.Select(p => p.Tipo));
    }

    [Fact]
    public async Task El_texto_libre_del_problema_no_viaja_en_el_mensaje()
    {
        var bus = new BusFalso();

        await Crear(bus).ObservarAsync(new BusquedaCreada(
            Guid.NewGuid(), Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
            "mi mail es ana@ejemplo.com y no arranca"));

        var datos = Assert.IsType<BusquedaCreadaV1>(Assert.Single(bus.Publicados).Datos);
        var json = Encoding.UTF8.GetString(SerializacionMensajes.Serializar(new MensajeIntegracion<BusquedaCreadaV1>
        {
            MensajeId = Guid.NewGuid(), Tipo = "busqueda.creada", OcurridoEn = DateTimeOffset.UtcNow, Datos = datos
        }));
        Assert.DoesNotContain("ana@ejemplo.com", json);
        Assert.DoesNotContain("arranca", json);
    }

    [Fact]
    public async Task Lleva_el_correlation_id_de_la_operacion_en_curso()
    {
        var bus = new BusFalso();

        using (ContextoCorrelacion.Establecer("corr-42"))
            await Crear(bus).ObservarAsync(new BusquedaCompartida(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("corr-42", Assert.Single(bus.Publicados).Correlacion);
    }

    [Fact]
    public async Task Con_el_broker_caido_la_operacion_sigue_igual()
    {
        var publicador = Crear(new BusFalso(caido: true));

        var error = await Record.ExceptionAsync(() =>
            publicador.ObservarAsync(new DerivacionATienda(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Easy", null)));

        Assert.Null(error);
    }

    [Fact]
    public void Publica_despues_del_observador_que_completa_la_busqueda()
    {
        var servicios = new ServiceCollection().AgregarAplicacion();

        var observadores = servicios
            .Where(d => d.ServiceType == typeof(IObservadorEventoDominio))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.Equal([typeof(ObservadorCompatibilidad), typeof(PublicadorEventos)], observadores);
    }

    [Fact]
    public void Sin_mensajeria_habilitada_el_bus_no_publica_nada()
    {
        var sinBroker = new ServiceCollection()
            .AddLogging()
            .AgregarAplicacion()
            .AgregarMensajeria(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        var conBroker = new ServiceCollection()
            .AddLogging()
            .AgregarAplicacion()
            .AgregarMensajeria(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Mensajeria:Habilitada"] = "true" })
                .Build())
            .BuildServiceProvider();

        Assert.IsType<BusDesactivado>(sinBroker.GetRequiredService<IEventBus>());
        Assert.IsType<BusRabbitMq>(conBroker.GetRequiredService<IEventBus>());
    }

    [Fact]
    public async Task Con_el_broker_inalcanzable_falla_rapido_y_despues_no_vuelve_a_esperar()
    {
        // Un puerto donde no escucha nadie: la primera vez espera el timeout de conexion, las
        // siguientes fallan al instante mientras dura la pausa. Asi un broker caido no hace
        // esperar a cada persona que comparte una consulta o sale a una tienda.
        var opciones = new OpcionesMensajeria { Habilitada = true, Host = "127.0.0.1", Puerto = 1, TimeoutSegundos = 2 };
        var conexion = new ConexionRabbitMq(opciones, NullLogger<ConexionRabbitMq>.Instance);
        await using var bus = new BusRabbitMq(conexion, opciones, NullLogger<BusRabbitMq>.Instance);
        var mensaje = new MensajeIntegracion<BusquedaCompartidaV1>
        {
            MensajeId = Guid.NewGuid(),
            Tipo = ClavesRuteo.BusquedaCompartida,
            OcurridoEn = DateTimeOffset.UtcNow,
            Datos = new BusquedaCompartidaV1 { BusquedaId = Guid.NewGuid(), ClienteId = Guid.NewGuid(), MecanicoId = Guid.NewGuid() }
        };

        var primera = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => bus.PublicarAsync(mensaje));
        primera.Stop();

        var segunda = Stopwatch.StartNew();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => bus.PublicarAsync(mensaje));
        segunda.Stop();

        Assert.True(primera.Elapsed < TimeSpan.FromSeconds(10), $"La primera tardo {primera.Elapsed}");
        Assert.IsType<BrokerNoDisponibleException>(error);
        Assert.True(segunda.Elapsed < TimeSpan.FromMilliseconds(500), $"La segunda tardo {segunda.Elapsed}");
    }
}
