using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RepMatch.Aplicacion.Catalogo;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Aplicacion.Validadores;
using RepMatch.Common.Logging;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.IA;
using RepMatch.IA.Llm;
using RepMatch.IA.Local;
using RepMatch.Mensajeria;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace RepMatch.Tests.Integracion;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiereDockerFactAttribute : FactAttribute
{
    public RequiereDockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("REPMATCH_TESTCONTAINERS") == "1")
            return;

        try
        {
            using var proceso = Process.Start(new ProcessStartInfo("docker")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "info", "--format", "{{.ServerVersion}}" }
            });

            if (proceso is null || !proceso.WaitForExit(15000) || proceso.ExitCode != 0)
                Skip = "La prueba de integración requiere un daemon Docker disponible.";
        }
        catch (Win32Exception)
        {
            Skip = "La prueba de integración requiere Docker; el CLI no está instalado.";
        }
        catch (InvalidOperationException)
        {
            Skip = "La prueba de integración no pudo iniciar el CLI de Docker.";
        }
    }
}

[CollectionDefinition("Flujo distribuido con PostgreSQL y RabbitMQ")]
public sealed class ColeccionFlujoDistribuido : ICollectionFixture<InfraestructuraDistribuidaFixture>
{
    public const string NombreColeccion = "Flujo distribuido con PostgreSQL y RabbitMQ";
}

public sealed class InfraestructuraDistribuidaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("repmatch")
        .WithUsername("repmatch")
        .WithPassword("repmatch")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("repmatch")
        .WithPassword("repmatch")
        .Build();

    public string CadenaPostgres => _postgres.GetConnectionString();

    public OpcionesMensajeria OpcionesRabbit => new()
    {
        Habilitada = true,
        Host = _rabbit.Hostname,
        Puerto = _rabbit.GetMappedPublicPort(5672),
        Usuario = "repmatch",
        Contrasena = "repmatch",
        TimeoutSegundos = 5
    };

    public async Task InitializeAsync() =>
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[Collection(ColeccionFlujoDistribuido.NombreColeccion)]
public sealed class FlujoDistribuidoTests(InfraestructuraDistribuidaFixture infraestructura)
{
    [RequiereDockerFact]
    public async Task Diagnostica_agrega_ofertas_publica_y_propaga_correlacion_al_consumidor()
    {
        await using var baseDatos = await BaseTemporal.CrearAsync(infraestructura.CadenaPostgres);
        await using var contexto = baseDatos.CrearContexto();
        var cliente = await CrearClienteAsync(contexto);
        var catalogo = CrearCatalogo(contexto);
        var opcionesRabbit = infraestructura.OpcionesRabbit;
        await using var conexion = new ConexionRabbitMq(opcionesRabbit, NullLogger<ConexionRabbitMq>.Instance);
        await using var bus = new BusRabbitMq(conexion, opcionesRabbit, NullLogger<BusRabbitMq>.Instance);

        using var serviciosConsumidor = new ServiceCollection().AddLogging().BuildServiceProvider();
        var consumidor = new ConsumidorBusqueda(
            conexion,
            opcionesRabbit,
            serviciosConsumidor.GetRequiredService<IServiceScopeFactory>());
        await consumidor.StartAsync(CancellationToken.None);

        var correlacion = $"integracion-{Guid.NewGuid():N}";
        ResultadoOperacion<BusquedaDto> creada;
        try
        {
            using (ContextoCorrelacion.Establecer(correlacion))
            {
                creada = await CrearServicioBusquedas(
                    contexto, catalogo, CrearDiagnosticadorConJsonInvalido(), bus)
                    .CrearAsync(new CrearBusquedaDto
                    {
                        ClienteId = cliente.Id,
                        Vehiculo = new VehiculoDto
                        {
                            Marca = "Volkswagen",
                            Modelo = "Gol",
                            Anio = 2015,
                            Motor = "1.6"
                        },
                        TextoLibre = "Vibra el volante y hace ruido cuando freno"
                    });
            }

            Assert.True(creada.EsExitoso, string.Join("; ", creada.Errores));
            Assert.Equal("Diagnosticada", creada.Valor.Estado);
            Assert.Equal(DiagnosticadorLocal.NombreFuente, creada.Valor.Diagnostico!.Fuente);
            Assert.NotEmpty(creada.Valor.CodigosObjetivo);

            var servicioOfertas = new ServicioOfertas(
                [new FuenteOfertasFija()],
                new BusquedaRepository(contexto),
                catalogo,
                new UnitOfWork(contexto),
                new OpcionesOfertas(),
                NullLogger<ServicioOfertas>.Instance);
            var ofertas = await servicioOfertas.ObtenerAsync(creada.Valor.Id, cliente.Id);

            Assert.True(ofertas.EsExitoso, string.Join("; ", ofertas.Errores));
            Assert.Equal("Completada", ofertas.Valor.Busqueda.Estado);
            Assert.Equal(creada.Valor.CodigosObjetivo.Count, ofertas.Valor.Busqueda.Ofertas.Count);

            var recibido = await consumidor.Recibido.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(correlacion, recibido.CorrelacionObservada);
            Assert.Equal(correlacion, recibido.Mensaje.CorrelationId);
            Assert.Equal(creada.Valor.Id, recibido.Mensaje.Datos.BusquedaId);

            var persistida = await contexto.Busquedas
                .Include(b => b.Ofertas)
                .SingleAsync(b => b.Id == creada.Valor.Id);
            Assert.Equal(creada.Valor.CodigosObjetivo, persistida.CodigosObjetivo);
            Assert.Equal(creada.Valor.CodigosObjetivo.Count, persistida.Ofertas.Count);
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }
    }

    [RequiereDockerFact]
    public async Task Un_broker_caido_no_revierte_una_busqueda_persistida()
    {
        await using var baseDatos = await BaseTemporal.CrearAsync(infraestructura.CadenaPostgres);
        await using var contexto = baseDatos.CrearContexto();
        var cliente = await CrearClienteAsync(contexto);
        var opcionesRabbit = new OpcionesMensajeria
        {
            Habilitada = true,
            Host = "127.0.0.1",
            Puerto = 1,
            TimeoutSegundos = 1,
            PausaTrasFallaSegundos = 1
        };
        await using var conexion = new ConexionRabbitMq(opcionesRabbit, NullLogger<ConexionRabbitMq>.Instance);
        await using var bus = new BusRabbitMq(conexion, opcionesRabbit, NullLogger<BusRabbitMq>.Instance);
        var resultado = await CrearServicioBusquedas(
                contexto, CrearCatalogo(contexto), new DiagnosticadorLocal(ClasificadorSintomas.CargarCorpusIncluido()), bus)
            .CrearAsync(new CrearBusquedaDto
            {
                ClienteId = cliente.Id,
                Vehiculo = new VehiculoDto { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015, Motor = "1.6" },
                TextoLibre = "Vibra el volante y hace ruido cuando freno"
            });

        Assert.True(resultado.EsExitoso, string.Join("; ", resultado.Errores));
        Assert.NotNull(await contexto.Busquedas.SingleOrDefaultAsync(b => b.Id == resultado.Valor.Id));
    }

    [RequiereDockerFact]
    public async Task Una_fuente_externa_que_agota_el_tiempo_se_informa_sin_fallar_la_busqueda()
    {
        await using var baseDatos = await BaseTemporal.CrearAsync(infraestructura.CadenaPostgres);
        await using var contexto = baseDatos.CrearContexto();
        var cliente = await CrearClienteAsync(contexto);
        var catalogo = CrearCatalogo(contexto);
        var creada = await CrearServicioBusquedas(
                contexto, catalogo, new DiagnosticadorLocal(ClasificadorSintomas.CargarCorpusIncluido()), new BusDesactivado(NullLogger<BusDesactivado>.Instance))
            .CrearAsync(new CrearBusquedaDto
            {
                ClienteId = cliente.Id,
                Vehiculo = new VehiculoDto { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015, Motor = "1.6" },
                TextoLibre = "Vibra el volante y hace ruido cuando freno"
            });

        Assert.True(creada.EsExitoso, string.Join("; ", creada.Errores));

        var servicioOfertas = new ServicioOfertas(
            [new FuenteColgada()],
            new BusquedaRepository(contexto),
            catalogo,
            new UnitOfWork(contexto),
            new OpcionesOfertas { TimeoutGlobalSegundos = 1 },
            NullLogger<ServicioOfertas>.Instance);
        var ofertas = await servicioOfertas.ObtenerAsync(creada.Valor.Id, cliente.Id);

        Assert.True(ofertas.EsExitoso, string.Join("; ", ofertas.Errores));
        Assert.Contains("Tienda lenta", ofertas.Valor.TiendasSinRespuesta);
    }

    private static ServicioBusquedas CrearServicioBusquedas(
        RepMatchDbContext contexto, ICatalogoRepuestos catalogo, IDiagnosticador diagnosticador, IEventBus bus)
    {
        var busquedas = new BusquedaRepository(contexto);
        var unidadDeTrabajo = new UnitOfWork(contexto);
        var observador = new ObservadorCompatibilidad(
            busquedas,
            new ServicioDiagnostico(catalogo, diagnosticador, NullLogger<ServicioDiagnostico>.Instance),
            unidadDeTrabajo,
            NullLogger<ObservadorCompatibilidad>.Instance);
        var despachador = new DespachadorEventos(
            [observador, new PublicadorEventos(bus, NullLogger<PublicadorEventos>.Instance)],
            NullLogger<DespachadorEventos>.Instance);

        return new ServicioBusquedas(
            busquedas,
            new ClienteRepository(contexto),
            unidadDeTrabajo,
            despachador,
            new ValidadorCrearBusqueda(),
            NullLogger<ServicioBusquedas>.Instance);
    }

    private static CatalogoLocal CrearCatalogo(RepMatchDbContext contexto) =>
        new(new RepuestoRepository(contexto), NullLogger<CatalogoLocal>.Instance);

    private static async Task<Cliente> CrearClienteAsync(RepMatchDbContext contexto)
    {
        var cliente = new Cliente("Cliente de integración", $"integracion-{Guid.NewGuid():N}@example.test");
        await contexto.Clientes.AddAsync(cliente);
        await contexto.SaveChangesAsync();
        return cliente;
    }

    private static IDiagnosticador CrearDiagnosticadorConJsonInvalido()
    {
        const string respuesta = """{"choices":[{"message":{"content":"esto no es JSON"}}]}""";
        var opciones = new OpcionesLlm
        {
            ApiKey = "clave-de-prueba",
            UrlBase = "https://llm.test/v1/"
        };
        var llm = new DiagnosticadorLlm(
            new HttpClient(new RespuestaLlmFalsa(respuesta)) { BaseAddress = new Uri(opciones.UrlBase) },
            opciones,
            NullLogger<DiagnosticadorLlm>.Instance);

        return new DiagnosticadorConRespaldo(
            llm,
            new DiagnosticadorLocal(ClasificadorSintomas.CargarCorpusIncluido()),
            opciones,
            NullLogger<DiagnosticadorConRespaldo>.Instance);
    }

    private sealed class ConsumidorBusqueda(
        ConexionRabbitMq conexion,
        OpcionesMensajeria opciones,
        IServiceScopeFactory scopes)
        : ConsumidorRabbitMq<BusquedaCreadaV1>(
            conexion, opciones, scopes, NullLogger<ConsumidorBusqueda>.Instance)
    {
        private readonly TaskCompletionSource<(MensajeIntegracion<BusquedaCreadaV1> Mensaje, string? CorrelacionObservada)> _recibido =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override DefinicionCola Cola => TopologiaRabbitMq.ColaActividad;

        public Task<(MensajeIntegracion<BusquedaCreadaV1> Mensaje, string? CorrelacionObservada)> Recibido =>
            _recibido.Task;

        protected override Task ProcesarAsync(
            IServiceProvider servicios, MensajeIntegracion<BusquedaCreadaV1> mensaje, CancellationToken ct)
        {
            _recibido.TrySetResult((mensaje, ContextoCorrelacion.Actual));
            return Task.CompletedTask;
        }
    }

    private sealed class RespuestaLlmFalsa(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class FuenteOfertasFija : IFuenteOfertas
    {
        public string Nombre => "Tienda de prueba";

        public Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OfertaExternaDto>>(
            [
                new OfertaExternaDto
                {
                    CodigoRepuesto = consulta.CodigoRepuesto,
                    Tienda = Nombre,
                    Titulo = consulta.Nombre,
                    Precio = 12345m,
                    Moneda = "ARS",
                    Url = $"https://tienda.example/{Uri.EscapeDataString(consulta.CodigoRepuesto)}",
                    Coincidencia = CoincidenciasOferta.PorCodigo
                }
            ]);
    }

    private sealed class FuenteColgada : IFuenteOfertas
    {
        public string Nombre => "Tienda lenta";

        public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }
    }

    private sealed class BaseTemporal(string cadenaConexion) : IAsyncDisposable
    {
        public static async Task<BaseTemporal> CrearAsync(string cadenaBase)
        {
            var constructor = new NpgsqlConnectionStringBuilder(cadenaBase)
            {
                Database = $"repmatch_{Guid.NewGuid():N}"
            };
            var baseTemporal = new BaseTemporal(constructor.ConnectionString);

            await using var contexto = baseTemporal.CrearContexto();
            await contexto.Database.EnsureCreatedAsync();
            await DatosSemilla.SembrarAsync(contexto);
            return baseTemporal;
        }

        public RepMatchDbContext CrearContexto() =>
            new(new DbContextOptionsBuilder<RepMatchDbContext>().UseNpgsql(cadenaConexion).Options);

        public async ValueTask DisposeAsync()
        {
            await using var contexto = CrearContexto();
            await contexto.Database.EnsureDeletedAsync();
        }
    }
}
