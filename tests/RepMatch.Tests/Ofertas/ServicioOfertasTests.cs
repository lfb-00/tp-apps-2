using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Ofertas;

/// <summary>
/// Servicio de Ofertas con tiendas simuladas y repositorios reales sobre InMemory: permisos por
/// rol, tienda caida, timeout global, deduplicacion, vencimiento y piezas del mecanico.
/// </summary>
public class ServicioOfertasTests
{
    private static readonly DatosVehiculo Gol = new("Volkswagen", "Gol", 2015, "1.6");

    private static readonly RepuestoDto Bateria = Pieza("BATE-12V-60AH", "Bateria 12V 60Ah");
    private static readonly RepuestoDto Pastillas = Pieza("PAST-VW-GOL-DEL", "Pastillas de freno delanteras");

    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static RepuestoDto Pieza(string codigo, string nombre) => new()
    {
        Id = Guid.NewGuid(),
        CodigoCanonico = codigo,
        Nombre = nombre,
        Sistema = "Electrico"
    };

    private static OfertaExternaDto OfertaConEan(string tienda, string referencia, string ean) => new()
    {
        CodigoRepuesto = Bateria.CodigoCanonico,
        Tienda = tienda,
        Titulo = $"Bateria 12V {tienda}",
        Precio = 100000m,
        Moneda = "ARS",
        Url = $"https://{tienda.ToLowerInvariant()}.example/{referencia}",
        Referencia = referencia,
        Ean = ean,
        Coincidencia = CoincidenciasOferta.ProbablePorNombre
    };

    /// <summary>Tienda que responde lo que se le configure por pieza, o que falla.</summary>
    private sealed class TiendaFalsa(string nombre) : IFuenteOfertas
    {
        public string Nombre => nombre;
        public Dictionary<string, List<OfertaExternaDto>> PorCodigo { get; } = [];
        public bool Caida { get; set; }
        public bool Colgada { get; set; }
        public List<string> Consultas { get; } = [];

        public async Task<IReadOnlyList<OfertaExternaDto>> BuscarAsync(ConsultaOfertasDto consulta, CancellationToken ct = default)
        {
            lock (Consultas)
                Consultas.Add(consulta.CodigoRepuesto);

            if (Colgada)
                await Task.Delay(Timeout.Infinite, ct);
            if (Caida)
                throw new HttpRequestException($"{nombre} no responde.");

            return PorCodigo.GetValueOrDefault(consulta.CodigoRepuesto) ?? [];
        }

        public TiendaFalsa Con(string codigo, string referencia, decimal precio)
        {
            if (!PorCodigo.TryGetValue(codigo, out var lista))
                PorCodigo[codigo] = lista = [];

            lista.Add(new OfertaExternaDto
            {
                CodigoRepuesto = codigo,
                Tienda = nombre,
                Titulo = $"{codigo} en {nombre}",
                Precio = precio,
                Moneda = "ARS",
                Url = $"https://{nombre.ToLowerInvariant()}.example/{referencia}",
                Referencia = referencia,
                Coincidencia = CoincidenciasOferta.ProbablePorNombre
            });
            return this;
        }
    }

    [Fact]
    public async Task Dos_tiendas_con_el_mismo_EAN_quedan_identificadas_como_el_mismo_producto()
    {
        var e = await SembrarAsync();
        var codigo = Bateria.CodigoCanonico;
        var easy = new TiendaFalsa("Easy");
        var fravega = new TiendaFalsa("Frávega");
        easy.PorCodigo[codigo] =
        [
            OfertaConEan("Easy", "1", "7797535000843")
        ];
        fravega.PorCodigo[codigo] =
        [
            OfertaConEan("Frávega", "2", "7797535000843")
        ];

        await using var contexto = CrearContexto();
        var resultado = await Crear(contexto, null, easy, fravega).ObtenerAsync(e.BusquedaId, e.ClienteId);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(2, resultado.Valor.Busqueda.Ofertas.Count);
        Assert.All(resultado.Valor.Busqueda.Ofertas,
            o => Assert.Equal(CoincidenciasOferta.ExactoPorEan, o.Coincidencia));
    }

    private sealed class CatalogoFalso : ICatalogoRepuestos
    {
        public string Modo => "Prueba";

        public Task<IReadOnlyList<RepuestoDto>> BuscarCompatiblesAsync(
            VehiculoDto vehiculo, string? sistema = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepuestoDto>>([Bateria, Pastillas]);

        public Task<RepuestoDto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default) =>
            Task.FromResult<RepuestoDto?>(null);

        public Task<IReadOnlyList<RepuestoDto>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepuestoDto>>([Bateria, Pastillas]);
    }

    private sealed class CatalogoVehiculosFalso : ICatalogoVehiculos
    {
        public Task<IReadOnlyList<OpcionVehiculoDto>> ListarOpcionesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OpcionVehiculoDto>>(
            [
                new OpcionVehiculoDto { Marca = "Volkswagen", Modelo = "Gol", AnioDesde = 2009, AnioHasta = 2019, Motor = "1.6" },
                new OpcionVehiculoDto { Marca = "Toyota", Modelo = "Corolla", AnioDesde = 2014, AnioHasta = 2026, Motor = "1.8" }
            ]);

        public Task<bool> EsConfiguracionValidaAsync(VehiculoDto vehiculo, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private static ServicioOfertas Crear(RepMatchDbContext contexto, OpcionesOfertas? opciones = null, params IFuenteOfertas[] tiendas) =>
        new(tiendas,
            new BusquedaRepository(contexto),
            new CatalogoFalso(),
            new CatalogoVehiculosFalso(),
            new UnitOfWork(contexto),
            opciones ?? new OpcionesOfertas(),
            NullLogger<ServicioOfertas>.Instance);

    private sealed record Escenario(Guid ClienteId, Guid MecanicoId, Guid BusquedaId);

    /// <summary>Una busqueda diagnosticada con la bateria, compartida con un mecanico.</summary>
    private async Task<Escenario> SembrarAsync()
    {
        await using var contexto = CrearContexto();
        var cliente = new Cliente("Ana", "ana@ejemplo.com");
        var mecanico = new Cliente("Carla", "carla@ejemplo.com", RolUsuario.Mecanico);
        await contexto.Clientes.AddRangeAsync(cliente, mecanico);

        var busqueda = new Busqueda(cliente.Id, Gol with { }, "no arranca a la manana, hace clic");
        busqueda.AsignarCodigosObjetivo([Bateria.CodigoCanonico]);
        busqueda.CompartirCon(mecanico.Id);
        await contexto.Busquedas.AddAsync(busqueda);
        await contexto.SaveChangesAsync();

        return new Escenario(cliente.Id, mecanico.Id, busqueda.Id);
    }

    [Fact]
    public async Task El_cliente_recibe_las_ofertas_de_todas_las_tiendas_y_quedan_guardadas()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);
        var fravega = new TiendaFalsa("Frávega").Con("BATE-12V-60AH", "2", 113000m);

        await using (var contexto = CrearContexto())
        {
            var resultado = await Crear(contexto, null, easy, fravega).ObtenerAsync(e.BusquedaId, e.ClienteId);

            Assert.True(resultado.EsExitoso);
            Assert.Equal(2, resultado.Valor.Busqueda.Ofertas.Count);
            Assert.Empty(resultado.Valor.TiendasSinRespuesta);
            Assert.NotNull(resultado.Valor.Busqueda.PreciosConsultadosEn);
            Assert.Equal("Completada", resultado.Valor.Busqueda.Estado);
        }

        await using (var contexto = CrearContexto())
        {
            var guardada = await contexto.Busquedas.Include(b => b.Ofertas).SingleAsync(b => b.Id == e.BusquedaId);
            Assert.Equal("Easy", guardada.OfertasOrdenadasPorPrecio()[0].NombreTienda);
        }
    }

    [Fact]
    public async Task Mientras_los_precios_estan_vigentes_no_vuelve_a_preguntar_a_las_tiendas()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);

        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);
        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);
        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId, forzar: true);

        // Una por la primera vez y otra por "Actualizar precios".
        Assert.Equal(2, easy.Consultas.Count);
    }

    [Fact]
    public async Task Una_tienda_caida_no_tumba_a_las_demas_ni_borra_sus_ofertas_viejas()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);
        var fravega = new TiendaFalsa("Frávega").Con("BATE-12V-60AH", "2", 113000m);

        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy, fravega).ObtenerAsync(e.BusquedaId, e.ClienteId);

        fravega.Caida = true;
        await using (var contexto = CrearContexto())
        {
            var resultado = await Crear(contexto, null, easy, fravega).ObtenerAsync(e.BusquedaId, e.ClienteId, forzar: true);

            Assert.True(resultado.EsExitoso);
            Assert.Equal(["Frávega"], resultado.Valor.TiendasSinRespuesta);
            // Que Fravega no conteste no quiere decir que se quedo sin stock.
            Assert.Equal(2, resultado.Valor.Busqueda.Ofertas.Count(o => o.Disponible));
        }
    }

    [Fact]
    public async Task Una_tienda_lenta_se_corta_con_el_tope_global_y_lo_demas_llega()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);
        var colgada = new TiendaFalsa("Lenta") { Colgada = true };

        await using var contexto = CrearContexto();
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var resultado = await Crear(contexto, new OpcionesOfertas { TimeoutGlobalSegundos = 1 }, easy, colgada)
            .ObtenerAsync(e.BusquedaId, e.ClienteId);

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal(["Lenta"], resultado.Valor.TiendasSinRespuesta);
        Assert.Single(resultado.Valor.Busqueda.Ofertas);
    }

    [Fact]
    public async Task Si_la_tienda_respondio_y_ya_no_la_publica_queda_no_disponible_con_el_mismo_id()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);

        Guid ofertaId;
        await using (var contexto = CrearContexto())
        {
            var primera = await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);
            ofertaId = Assert.Single(primera.Valor.Busqueda.Ofertas).Id;
        }

        easy.PorCodigo.Clear();
        await using (var contexto = CrearContexto())
        {
            var segunda = await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId, forzar: true);

            // Mismo id: una derivacion vieja sigue apuntando a una oferta que existe.
            var oferta = Assert.Single(segunda.Valor.Busqueda.Ofertas);
            Assert.Equal(ofertaId, oferta.Id);
            Assert.False(oferta.Disponible);
        }
    }

    [Fact]
    public async Task Una_captura_nueva_de_la_misma_publicacion_actualiza_el_precio_sin_duplicar()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);

        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);

        easy.PorCodigo.Clear();
        easy.Con("BATE-12V-60AH", "1", 95000m);
        await using (var contexto = CrearContexto())
        {
            var resultado = await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId, forzar: true);
            Assert.Equal(95000m, Assert.Single(resultado.Valor.Busqueda.Ofertas).Precio);
        }
    }

    [Fact]
    public async Task El_mecanico_asignado_ve_los_precios_y_un_tercero_no()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m);

        await using var contexto = CrearContexto();
        var servicio = Crear(contexto, null, easy);

        Assert.True((await servicio.ObtenerAsync(e.BusquedaId, e.MecanicoId)).EsExitoso);
        Assert.True((await servicio.ObtenerAsync(e.BusquedaId, Guid.NewGuid())).EsFallido);
    }

    [Fact]
    public async Task Si_el_mecanico_recomienda_otra_pieza_se_buscan_solo_sus_precios()
    {
        var e = await SembrarAsync();
        var easy = new TiendaFalsa("Easy")
            .Con("BATE-12V-60AH", "1", 100810m)
            .Con("PAST-VW-GOL-DEL", "9", 30000m);

        await using (var contexto = CrearContexto())
            await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);

        await using (var contexto = CrearContexto())
        {
            var busqueda = await contexto.Busquedas.Include(b => b.Ofertas).SingleAsync(b => b.Id == e.BusquedaId);
            busqueda.RegistrarRevision(e.MecanicoId, confirmaDiagnostico: false, ["PAST-VW-GOL-DEL"], "Son las pastillas");
            await contexto.SaveChangesAsync();
        }

        easy.Consultas.Clear();
        await using (var contexto = CrearContexto())
        {
            var resultado = await Crear(contexto, null, easy).ObtenerAsync(e.BusquedaId, e.ClienteId);

            Assert.Equal(["PAST-VW-GOL-DEL"], easy.Consultas);
            Assert.Contains(resultado.Valor.Busqueda.Ofertas, o => o.CodigoRepuesto == "PAST-VW-GOL-DEL");
        }
    }

    [Fact]
    public async Task Sin_tiendas_configuradas_responde_sin_precios()
    {
        var e = await SembrarAsync();

        await using var contexto = CrearContexto();
        var resultado = await Crear(contexto).ObtenerAsync(e.BusquedaId, e.ClienteId);

        Assert.True(resultado.EsExitoso);
        Assert.Empty(resultado.Valor.Busqueda.Ofertas);
        Assert.Null(resultado.Valor.Busqueda.PreciosConsultadosEn);
    }

    [Fact]
    public async Task El_resumen_sin_sesion_dice_desde_cuanto_y_en_cuantas_tiendas_sin_guardar_nada()
    {
        var easy = new TiendaFalsa("Easy").Con("BATE-12V-60AH", "1", 100810m).Con("BATE-12V-60AH", "3", 127287.5m);
        var fravega = new TiendaFalsa("Frávega").Con("BATE-12V-60AH", "2", 113000m);

        await using var contexto = CrearContexto();
        var resumen = await Crear(contexto, null, easy, fravega).ResumirAsync([Bateria, Pastillas], Gol.ADto());

        var bateria = Assert.Single(resumen.Piezas, p => p.CodigoRepuesto == "BATE-12V-60AH");
        Assert.Equal(100810m, bateria.Desde);
        Assert.Equal(2, bateria.Tiendas);
        Assert.Null(Assert.Single(resumen.Piezas, p => p.CodigoRepuesto == "PAST-VW-GOL-DEL").Desde);
        Assert.Empty(contexto.Ofertas);
    }
}
