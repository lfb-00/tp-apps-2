using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Aplicacion;

/// <summary>
/// Quien compara sin cuenta tambien sale a la tienda, y esa salida es la comision. Sin busqueda ni
/// oferta guardadas, la derivacion guarda la pieza, la tienda y el destino, y nada de la persona.
/// </summary>
public class DerivacionSinCuentaTests
{
    private const string Url = "https://www.autodo.com.ar/mann-filter-w712-83/p";
    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    /// <summary>Anota lo que se despacha: el evento es lo que llega a la cola y a las comisiones.</summary>
    private sealed class Anotador : IObservadorEventoDominio
    {
        public List<IEventoDominio> Recibidos { get; } = [];
        public bool PuedeObservar(IEventoDominio evento) => true;

        public Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default)
        {
            Recibidos.Add(evento);
            return Task.CompletedTask;
        }
    }

    private static ServicioDerivaciones Crear(RepMatchDbContext contexto, IObservadorEventoDominio observador) =>
        new(new BusquedaRepository(contexto),
            new DerivacionRepository(contexto),
            new UnitOfWork(contexto),
            new DespachadorEventos([observador], NullLogger<DespachadorEventos>.Instance),
            NullLogger<ServicioDerivaciones>.Instance);

    [Fact]
    public async Task Registra_la_salida_sin_busqueda_ni_persona_y_avisa_a_la_cola()
    {
        var anotador = new Anotador();

        await using (var contexto = CrearContexto())
        {
            var resultado = await Crear(contexto, anotador).DerivarSinCuentaAsync("FILT-ACE-TOY-HILUX-28", "Autodo", Url);
            Assert.True(resultado.EsExitoso);
            Assert.Equal(Url, resultado.Valor);
        }

        await using (var contexto = CrearContexto())
        {
            var derivacion = Assert.Single(await contexto.Derivaciones.ToListAsync());
            Assert.Null(derivacion.BusquedaId);
            Assert.Null(derivacion.OfertaId);
            Assert.Null(derivacion.MecanicoId);
            Assert.Equal("FILT-ACE-TOY-HILUX-28", derivacion.CodigoRepuesto);
            Assert.Equal("Autodo", derivacion.NombreTienda);
        }

        var evento = Assert.IsType<DerivacionATienda>(Assert.Single(anotador.Recibidos));
        Assert.Null(evento.BusquedaId);
        Assert.Equal("Autodo", evento.NombreTienda);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://tienda.test/archivo")]
    [InlineData("/relativa")]
    public async Task Solo_acepta_destinos_http_o_https(string url)
    {
        await using var contexto = CrearContexto();

        var resultado = await Crear(contexto, new Anotador()).DerivarSinCuentaAsync("BATE-12V-50AH", "Autodo", url);

        Assert.False(resultado.EsExitoso);
        Assert.Empty(await contexto.Derivaciones.ToListAsync());
    }

    [Fact]
    public void La_derivacion_de_una_busqueda_guarda_tambien_la_pieza()
    {
        var busqueda = new Busqueda(Guid.NewGuid(), new Domain.ValueObjects.DatosVehiculo("Toyota", "Hilux", 2018, "2.8 TDI"),
            "toca hacer el service de filtros");
        busqueda.AsignarCodigosObjetivo(["FILT-ACE-TOY-HILUX-28"]);
        busqueda.ActualizarOfertas(["FILT-ACE-TOY-HILUX-28"], ["Autodo"],
        [
            new Oferta(busqueda.Id, "FILT-ACE-TOY-HILUX-28", "Autodo", "Filtro aceite Hilux",
                new Domain.ValueObjects.Dinero(11041.88m, "ARS"), Url)
        ]);

        var derivacion = busqueda.Derivar(busqueda.Ofertas.Single().Id);

        Assert.Equal(busqueda.Id, derivacion.BusquedaId);
        Assert.Equal("FILT-ACE-TOY-HILUX-28", derivacion.CodigoRepuesto);
    }
}
