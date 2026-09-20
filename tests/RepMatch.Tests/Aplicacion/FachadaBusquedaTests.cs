using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Fachadas;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Aplicacion.Validadores;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Tests.Aplicacion;

/// <summary>
/// La fachada del caso de uso de busqueda no tiene reglas propias: lo que se verifica aca es la
/// orquestacion. Que los pasos ocurran en el orden correcto, que un paso fallido corte el flujo
/// antes de tocar el siguiente colaborador, y que una caida del catalogo remoto llegue a la
/// pantalla como un resultado legible y no como una excepcion.
///
/// Se usan dobles en memoria de los repositorios y del catalogo, con los servicios y validadores
/// reales por debajo: asi el test cubre la fachada junto con la logica de negocio que coordina.
/// </summary>
public class FachadaBusquedaTests
{
    private readonly ClientesEnMemoria _clientes = new();
    private readonly BusquedasEnMemoria _busquedas = new();
    private readonly UnidadDeTrabajoEspia _unidadDeTrabajo = new();
    private readonly CatalogoEspia _catalogo = new();
    private readonly Cliente _ana = new("Ana", "ana@ejemplo.com");

    private static readonly VehiculoDto Gol = new()
    {
        Marca = "Volkswagen", Modelo = "Gol", Anio = 2015, Motor = "1.6"
    };

    public FachadaBusquedaTests() => _clientes.Sembrar(_ana);

    private FachadaBusqueda CrearFachada()
    {
        var servicioBusquedas = new ServicioBusquedas(
            _busquedas, _clientes, _unidadDeTrabajo,
            new ValidadorCrearBusqueda(), NullLogger<ServicioBusquedas>.Instance);

        var servicioClientes = new ServicioClientes(
            _clientes, _unidadDeTrabajo,
            new ValidadorCrearCliente(), NullLogger<ServicioClientes>.Instance);

        return new FachadaBusqueda(
            servicioBusquedas, servicioClientes, _catalogo, NullLogger<FachadaBusqueda>.Instance);
    }

    private CrearBusquedaDto SolicitudDeAna(string texto = "Cuando freno vibra el volante") => new()
    {
        ClienteId = _ana.Id,
        Vehiculo = Gol,
        TextoLibre = texto
    };

    private static RepuestoDto Repuesto(string codigo) => new()
    {
        Id = Guid.NewGuid(),
        CodigoCanonico = codigo,
        Nombre = codigo,
        Sistema = "Frenos"
    };

    [Fact]
    public async Task Registra_la_busqueda_con_los_codigos_que_devuelve_el_catalogo()
    {
        _catalogo.Compatibles.AddRange([Repuesto("PAST-VW-GOL-DEL"), Repuesto("DISC-VW-GOL-256")]);

        var resultado = await CrearFachada().ResolverBusquedaAsync(SolicitudDeAna());

        Assert.True(resultado.EsExitoso, string.Join("; ", resultado.Errores));
        Assert.Equal("Diagnosticada", resultado.Valor.Estado);
        Assert.Equal(["PAST-VW-GOL-DEL", "DISC-VW-GOL-256"], resultado.Valor.CodigosObjetivo);

        var consulta = Assert.Single(_catalogo.Consultas);
        Assert.Equal(Gol, consulta);

        var persistida = Assert.Single(_busquedas.Todas);
        Assert.Equal(resultado.Valor.Id, persistida.Id);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Deja_la_busqueda_fallida_cuando_el_catalogo_no_tiene_compatibles()
    {
        // Sin repuestos catalogados la operacion no falla: la busqueda se registra igual, pero
        // en estado Fallida y con motivo. Esa decision es del servicio, no de la fachada.
        var resultado = await CrearFachada().ResolverBusquedaAsync(SolicitudDeAna());

        Assert.True(resultado.EsExitoso);
        Assert.Equal("Fallida", resultado.Valor.Estado);
        Assert.Empty(resultado.Valor.CodigosObjetivo);

        var persistida = Assert.Single(_busquedas.Todas);
        Assert.Contains("Volkswagen Gol 2015", persistida.MotivoFalla);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Rechaza_una_solicitud_invalida_sin_consultar_el_catalogo()
    {
        var resultado = await CrearFachada().ResolverBusquedaAsync(SolicitudDeAna(texto: "hum"));

        Assert.True(resultado.EsFallido);
        Assert.Contains(resultado.Errores, e => e.Contains("mas de detalle"));

        Assert.Empty(_catalogo.Consultas);
        Assert.Empty(_busquedas.Todas);
        Assert.Equal(0, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Rechaza_un_cliente_inexistente_sin_consultar_el_catalogo()
    {
        var desconocido = Guid.NewGuid();
        var solicitud = SolicitudDeAna() with { ClienteId = desconocido };

        var resultado = await CrearFachada().ResolverBusquedaAsync(solicitud);

        Assert.True(resultado.EsFallido);
        Assert.Equal($"No existe el cliente {desconocido}.", resultado.Error);

        Assert.Empty(_catalogo.Consultas);
        Assert.Empty(_busquedas.Todas);
    }

    public static TheoryData<Exception> FallasDelSaltoRemoto() => new()
    {
        new HttpRequestException("Connection refused (localhost:5081)"),
        new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")
    };

    [Theory]
    [MemberData(nameof(FallasDelSaltoRemoto))]
    public async Task Traduce_la_caida_del_catalogo_remoto_a_un_resultado_fallido(Exception falla)
    {
        _catalogo.AntesDeResponder = _ => throw falla;

        var resultado = await CrearFachada().ResolverBusquedaAsync(SolicitudDeAna());

        Assert.True(resultado.EsFallido);
        Assert.StartsWith("No se pudo consultar el catalogo (Prueba)", resultado.Error);
        Assert.Contains(falla.Message, resultado.Error);

        // Si el catalogo no respondio, no se registra nada: no hay busqueda a medias.
        Assert.Empty(_busquedas.Todas);
        Assert.Equal(0, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Propaga_la_cancelacion_pedida_por_el_llamador()
    {
        // Cuando el que cancela es el llamador (el circuito Blazor que se cierra), la fachada no
        // debe disfrazarlo de "catalogo caido": la cancelacion se propaga tal cual.
        using var cts = new CancellationTokenSource();
        _catalogo.AntesDeResponder = ct =>
        {
            cts.Cancel();
            throw new OperationCanceledException(ct);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CrearFachada().ResolverBusquedaAsync(SolicitudDeAna(), cts.Token));

        Assert.Empty(_busquedas.Todas);
    }

    [Fact]
    public async Task Las_consultas_de_apoyo_delegan_en_los_servicios()
    {
        _catalogo.Compatibles.Add(Repuesto("PAST-VW-GOL-DEL"));
        var fachada = CrearFachada();

        var registrada = await fachada.ResolverBusquedaAsync(SolicitudDeAna());

        var clientes = await fachada.ListarClientesAsync();
        var recientes = await fachada.ListarRecientesAsync();

        Assert.Equal(_ana.Id, Assert.Single(clientes).Id);
        Assert.Equal(registrada.Valor.Id, Assert.Single(recientes).Id);
        Assert.Equal("Prueba", fachada.ModoCatalogo);
    }

    // ── Dobles en memoria ──────────────────────────────────────────────────────────────────────────

    private sealed class ClientesEnMemoria : IClienteRepository
    {
        private readonly List<Cliente> _items = [];

        public void Sembrar(Cliente cliente) => _items.Add(cliente);

        public Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.SingleOrDefault(c => c.Id == id));

        public Task<Cliente?> ObtenerPorEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_items.SingleOrDefault(c => c.Email == email));

        public Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Cliente>>([.. _items]);

        public Task AgregarAsync(Cliente cliente, CancellationToken ct = default)
        {
            _items.Add(cliente);
            return Task.CompletedTask;
        }

        public void Eliminar(Cliente cliente) => _items.Remove(cliente);
    }

    private sealed class BusquedasEnMemoria : IBusquedaRepository
    {
        public List<Busqueda> Todas { get; } = [];

        public Task<Busqueda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Todas.SingleOrDefault(b => b.Id == id));

        public Task<IReadOnlyList<Busqueda>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Busqueda>>([.. Todas.Where(b => b.ClienteId == clienteId)]);

        public Task<IReadOnlyList<Busqueda>> ListarRecientesAsync(int cantidad = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Busqueda>>(
                [.. Todas.OrderByDescending(b => b.FechaCreacion).Take(cantidad)]);

        public Task AgregarAsync(Busqueda busqueda, CancellationToken ct = default)
        {
            Todas.Add(busqueda);
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadDeTrabajoEspia : IUnitOfWork
    {
        public int Confirmaciones { get; private set; }

        public Task<int> ConfirmarAsync(CancellationToken ct = default)
        {
            Confirmaciones++;
            return Task.FromResult(1);
        }
    }

    /// <summary>Catalogo controlable: registra cada consulta y permite simular una falla del salto
    /// remoto justo antes de responder.</summary>
    private sealed class CatalogoEspia : ICatalogoRepuestos
    {
        public string Modo => "Prueba";

        public List<RepuestoDto> Compatibles { get; } = [];
        public List<VehiculoDto> Consultas { get; } = [];
        public Action<CancellationToken>? AntesDeResponder { get; set; }

        public Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
            VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default)
        {
            Consultas.Add(vehiculo);
            AntesDeResponder?.Invoke(ct);
            return Task.FromResult<IReadOnlyList<RepuestoDto>>([.. Compatibles]);
        }

        public Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default) =>
            Task.FromResult(Compatibles.SingleOrDefault(r => r.CodigoCanonico == codigoCanonico));

        public Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepuestoDto>>([.. Compatibles]);
    }
}
