extern alias IAApi;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Clientes.Rest;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.IA;

/// <summary>
/// La IA dentro de la busqueda: la compatibilidad sigue siendo el filtro, la IA elige entre esas
/// piezas, y si la IA falla la busqueda sale como en la Primera Parte.
/// </summary>
public class BusquedaConIATests
{
    private static readonly IReadOnlyList<RepuestoDto> PiezasGol =
    [.. CandidatosGol.Todos.Select(c => new RepuestoDto
    {
        Id = Guid.NewGuid(),
        CodigoCanonico = c.Codigo,
        Nombre = c.Nombre,
        Sistema = c.Sistema
    })];

    private static ServicioDiagnostico Servicio(IDiagnosticador diagnosticador) =>
        new(new CatalogoFijo(PiezasGol), diagnosticador, NullLogger<ServicioDiagnostico>.Instance);

    [Fact]
    public async Task La_IA_elige_entre_las_piezas_compatibles()
    {
        var ia = new DiagnosticadorFijo("Frenos", ["DISC-VW-GOL-256", "NO-EXISTE"]);

        var piezas = await Servicio(ia).DiagnosticarAsync(CandidatosGol.Vehiculo, "vibra al frenar");

        Assert.Equal(["DISC-VW-GOL-256"], piezas.Elegidas.Select(p => p.CodigoCanonico));
        Assert.Equal(PiezasGol.Count, ia.Recibida!.Candidatos.Count);
    }

    [Fact]
    public async Task Si_la_IA_no_elige_ninguna_quedan_todas_las_compatibles()
    {
        var piezas = await Servicio(new DiagnosticadorFijo("Transmision", [])).DiagnosticarAsync(
            CandidatosGol.Vehiculo, "patina el embrague");

        Assert.Equal(PiezasGol.Count, piezas.Elegidas.Count);
        Assert.Equal("Transmision", piezas.Diagnostico!.Sistema);
    }

    [Fact]
    public async Task Si_la_IA_esta_caida_la_busqueda_sale_por_compatibilidad()
    {
        var piezas = await Servicio(new DiagnosticadorCaido()).DiagnosticarAsync(CandidatosGol.Vehiculo, "ruido al frenar");

        Assert.Null(piezas.Diagnostico);
        Assert.Equal(PiezasGol.Count, piezas.Elegidas.Count);
    }

    [Fact]
    public async Task Sin_texto_no_consulta_a_la_IA()
    {
        var ia = new DiagnosticadorFijo("Frenos", []);

        await Servicio(ia).DiagnosticarAsync(CandidatosGol.Vehiculo, "  ");

        Assert.Null(ia.Recibida);
    }

    [Fact]
    public async Task El_observador_guarda_el_diagnostico_y_los_codigos_elegidos()
    {
        var nombreBase = Guid.NewGuid().ToString();
        var busqueda = new Busqueda(Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"), "vibra el volante al frenar");

        await using (var contexto = CrearContexto(nombreBase))
        {
            await contexto.Busquedas.AddAsync(busqueda);
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = CrearContexto(nombreBase))
        {
            var observador = new ObservadorCompatibilidad(
                new BusquedaRepository(contexto),
                Servicio(new DiagnosticadorFijo("Frenos", ["DISC-VW-GOL-256"])),
                new UnitOfWork(contexto),
                NullLogger<ObservadorCompatibilidad>.Instance);

            await observador.ObservarAsync(new BusquedaCreada(busqueda.Id, busqueda.ClienteId, busqueda.Vehiculo, busqueda.TextoLibre));
        }

        await using (var contexto = CrearContexto(nombreBase))
        {
            var dto = (await contexto.Busquedas.SingleAsync(b => b.Id == busqueda.Id)).ADto();

            Assert.Equal(["DISC-VW-GOL-256"], dto.CodigosObjetivo);
            Assert.Equal("Frenos", dto.Diagnostico!.Sistema);
            Assert.Equal("Alta", dto.Diagnostico.Urgencia);
            Assert.Equal(["Discos alabeados"], dto.Diagnostico.Causas);
            Assert.Equal("prueba", dto.Diagnostico.Fuente);
        }
    }

    private static RepMatchDbContext CrearContexto(string nombreBase) =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private sealed class CatalogoFijo(IReadOnlyList<RepuestoDto> piezas) : ICatalogoRepuestos
    {
        public string Modo => "Prueba";

        public Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
            VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default) => Task.FromResult(piezas);

        public Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default) =>
            Task.FromResult(piezas.FirstOrDefault(p => p.CodigoCanonico == codigoCanonico));

        public Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default) => Task.FromResult(piezas);
    }

    private sealed class DiagnosticadorFijo(string sistema, IReadOnlyList<string> codigos) : IDiagnosticador
    {
        public SolicitudDiagnosticoDto? Recibida { get; private set; }
        public string Modo => "Prueba";

        public Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default)
        {
            Recibida = solicitud;
            return Task.FromResult(new DiagnosticoDto
            {
                Sistema = sistema,
                Urgencia = "Alta",
                Causas = [new CausaProbableDto { Causa = "Discos alabeados", Confianza = 0.8 }],
                Codigos = codigos,
                Fuente = "prueba",
                LatenciaMs = 1
            });
        }
    }

    private sealed class DiagnosticadorCaido : IDiagnosticador
    {
        public string Modo => "Remoto";

        public Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default) =>
            throw new HttpRequestException("IA.Api no responde");
    }
}

/// <summary>
/// IA.Api hospedado en memoria, sin clave de LLM: el servicio REST responde con el clasificador
/// local, valida la entrada y el cliente remoto de la Web lo consume con el mismo contrato.
/// </summary>
public class IAApiTests : IClassFixture<WebApplicationFactory<IAApi::Program>>
{
    private readonly HttpClient _http;

    public IAApiTests(WebApplicationFactory<IAApi::Program> fabrica)
    {
        // Aunque la maquina tenga IA__Llm__ApiKey, la prueba no sale a la red.
        _http = fabrica.WithWebHostBuilder(b => b.UseSetting("IA:Llm:ApiKey", "")).CreateClient();
    }

    [Fact]
    public async Task Diagnostica_por_REST_con_el_respaldo_local()
    {
        var respuesta = await _http.PostAsJsonAsync("api/diagnostico", CandidatosGol.Solicitud("hace un ruido al frenar"));

        respuesta.EnsureSuccessStatusCode();
        var diagnostico = await respuesta.Content.ReadFromJsonAsync<DiagnosticoDto>();

        Assert.Equal("Frenos", diagnostico!.Sistema);
        Assert.Equal("fallback-local", diagnostico.Fuente);
        Assert.All(diagnostico.Codigos, c => Assert.Contains(CandidatosGol.Todos, x => x.Codigo == c));
    }

    [Fact]
    public async Task El_cliente_remoto_de_la_Web_devuelve_lo_mismo_que_la_API()
    {
        var remoto = new DiagnosticadorRemoto(_http, NullLogger<DiagnosticadorRemoto>.Instance);

        var diagnostico = await remoto.DiagnosticarAsync(CandidatosGol.Solicitud("vibra el volante cuando freno"));

        Assert.Equal("Remoto", remoto.Modo);
        Assert.Equal("DISC-VW-GOL-256", diagnostico.Codigos[0]);
    }

    [Fact]
    public async Task Rechaza_un_texto_demasiado_corto()
    {
        var respuesta = await _http.PostAsJsonAsync("api/diagnostico", CandidatosGol.Solicitud("ok"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Salud_dice_que_responde_el_clasificador_local()
    {
        var salud = await _http.GetStringAsync("/salud");

        Assert.Contains("clasificador local", salud);
    }
}
