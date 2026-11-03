using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Aplicacion.Validadores;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Aplicacion;

/// <summary>
/// Colaboracion cliente-mecanico y derivacion contra repositorios reales sobre InMemory: los
/// permisos por rol y el limite del plan se chequean en la capa de servicios.
/// </summary>
public class ServicioRevisionesTests
{
    private const string Contrasena = "repMatch123!";
    private static readonly DatosVehiculo Gol = new("Volkswagen", "Gol", 2015, "1.6");

    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static DespachadorEventos SinObservadores() =>
        new([], NullLogger<DespachadorEventos>.Instance);

    private static ServicioRevisiones CrearRevisiones(RepMatchDbContext contexto) =>
        new(new BusquedaRepository(contexto),
            new ClienteRepository(contexto),
            new DerivacionRepository(contexto),
            new UnitOfWork(contexto),
            SinObservadores(),
            new ValidadorRevisarBusqueda(),
            NullLogger<ServicioRevisiones>.Instance);

    private static ServicioDerivaciones CrearDerivaciones(RepMatchDbContext contexto) =>
        new(new BusquedaRepository(contexto),
            new DerivacionRepository(contexto),
            new UnitOfWork(contexto),
            SinObservadores(),
            NullLogger<ServicioDerivaciones>.Instance);

    private static ServicioClientes CrearClientes(RepMatchDbContext contexto) =>
        new(new ClienteRepository(contexto),
            new BusquedaRepository(contexto),
            new UnitOfWork(contexto),
            new ValidadorCrearCliente(),
            new ValidadorVehiculo(),
            new ValidadorActualizarPerfil(),
            new ValidadorCambiarContrasena(),
            new ValidadorCambiarFotoPerfil(),
            new ValidadorEliminarCuenta(),
            new ConfiguracionVehiculoRepository(contexto),
            NullLogger<ServicioClientes>.Instance);

    private sealed record Escenario(Guid ClienteId, Guid MecanicoId, Guid OtroClienteId, IReadOnlyList<Guid> Busquedas);

    /// <summary>Un cliente con varias busquedas diagnosticadas, un mecanico gratuito y otro
    /// cliente que no deberia ver nada ajeno.</summary>
    private async Task<Escenario> SembrarAsync(int cantidadBusquedas = 1)
    {
        await using var contexto = CrearContexto();

        var cliente = new Cliente("Ana", "ana@ejemplo.com");
        var mecanico = new Cliente("Carla", "carla@ejemplo.com", RolUsuario.Mecanico);
        mecanico.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword(Contrasena));
        var otro = new Cliente("Beto", "beto@ejemplo.com");
        await contexto.Clientes.AddRangeAsync(cliente, mecanico, otro);

        var ids = new List<Guid>();
        for (var i = 0; i < cantidadBusquedas; i++)
        {
            // Una instancia de DatosVehiculo por busqueda: EF no admite compartir un owned entre duenios.
            var busqueda = new Busqueda(cliente.Id, Gol with { }, $"vibra el volante al frenar {i}");
            busqueda.AsignarCodigosObjetivo(["PAST-VW-GOL-DEL"]);
            busqueda.RegistrarOfertas([new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "Tienda A",
                "Pastillas Gol", Dinero.Pesos(25000), "https://tienda-a.example/pastillas")]);
            await contexto.Busquedas.AddAsync(busqueda);
            ids.Add(busqueda.Id);
        }

        await contexto.SaveChangesAsync();
        return new Escenario(cliente.Id, mecanico.Id, otro.Id, ids);
    }

    [Fact]
    public async Task Registrar_como_mecanico_arranca_en_plan_gratuito()
    {
        await using var contexto = CrearContexto();

        var resultado = await CrearClientes(contexto).RegistrarAsync(new CrearClienteDto
        {
            Nombre = "Taller Prueba",
            Email = "taller@ejemplo.com",
            Contrasena = Contrasena,
            RepetirContrasena = Contrasena,
            Rol = RolesUsuario.Mecanico
        });

        Assert.True(resultado.EsExitoso);
        Assert.True(resultado.Valor.EsMecanico);
        Assert.Equal(PlanesMecanico.Gratuito, resultado.Valor.Plan);
    }

    [Fact]
    public async Task No_se_puede_compartir_con_alguien_que_no_es_mecanico()
    {
        var escenario = await SembrarAsync();
        await using var contexto = CrearContexto();

        var resultado = await CrearRevisiones(contexto)
            .CompartirAsync(escenario.Busquedas[0], escenario.ClienteId, escenario.OtroClienteId);

        Assert.True(resultado.EsFallido);
    }

    [Fact]
    public async Task El_plan_gratuito_rechaza_la_consulta_que_excede_el_limite()
    {
        var escenario = await SembrarAsync(Cliente.MaximoPendientesPlanGratuito + 1);

        await using (var contexto = CrearContexto())
        {
            var servicio = CrearRevisiones(contexto);
            foreach (var id in escenario.Busquedas.Take(Cliente.MaximoPendientesPlanGratuito))
                Assert.True((await servicio.CompartirAsync(id, escenario.ClienteId, escenario.MecanicoId)).EsExitoso);

            var excedida = await servicio.CompartirAsync(escenario.Busquedas[^1], escenario.ClienteId, escenario.MecanicoId);
            Assert.True(excedida.EsFallido);
        }

        await using (var contexto = CrearContexto())
        {
            var mecanico = await contexto.Clientes.SingleAsync(c => c.Id == escenario.MecanicoId);
            mecanico.CambiarPlan(PlanMecanico.Pro);
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = CrearContexto())
        {
            var conPro = await CrearRevisiones(contexto)
                .CompartirAsync(escenario.Busquedas[^1], escenario.ClienteId, escenario.MecanicoId);
            Assert.True(conPro.EsExitoso);
        }
    }

    [Fact]
    public async Task La_consulta_la_ven_solo_el_duenio_y_el_mecanico_asignado()
    {
        var escenario = await SembrarAsync();
        var id = escenario.Busquedas[0];

        await using var contexto = CrearContexto();
        var servicio = CrearRevisiones(contexto);

        Assert.Null(await servicio.ObtenerAsync(id, escenario.MecanicoId));
        await servicio.CompartirAsync(id, escenario.ClienteId, escenario.MecanicoId);

        Assert.NotNull(await servicio.ObtenerAsync(id, escenario.ClienteId));
        Assert.Equal("Ana", (await servicio.ObtenerAsync(id, escenario.MecanicoId))?.NombreCliente);
        Assert.Null(await servicio.ObtenerAsync(id, escenario.OtroClienteId));
    }

    [Fact]
    public async Task La_derivacion_de_lo_recomendado_queda_atribuida_al_mecanico()
    {
        var escenario = await SembrarAsync();
        var id = escenario.Busquedas[0];

        await using (var contexto = CrearContexto())
        {
            var servicio = CrearRevisiones(contexto);
            await servicio.CompartirAsync(id, escenario.ClienteId, escenario.MecanicoId);
            var revision = await servicio.RevisarAsync(id, escenario.MecanicoId,
                new RevisarBusquedaDto { ConfirmaDiagnostico = true });
            Assert.True(revision.EsExitoso);
        }

        Guid ofertaId;
        await using (var contexto = CrearContexto())
            ofertaId = (await contexto.Ofertas.SingleAsync(o => o.BusquedaId == id)).Id;

        await using (var contexto = CrearContexto())
        {
            var url = await CrearDerivaciones(contexto).DerivarAsync(ofertaId);
            Assert.Equal("https://tienda-a.example/pastillas", url.Valor);
        }

        await using (var contexto = CrearContexto())
        {
            var bandeja = await CrearRevisiones(contexto).ObtenerBandejaAsync(escenario.MecanicoId);
            Assert.Equal(1, bandeja.Valor.DerivacionesGeneradas);
            Assert.Equal(0, bandeja.Valor.Pendientes);
            Assert.Equal(Cliente.MaximoPendientesPlanGratuito, bandeja.Valor.LimitePendientes);
        }
    }

    [Fact]
    public async Task Si_el_mecanico_borra_la_cuenta_sus_pendientes_vuelven_al_cliente()
    {
        var escenario = await SembrarAsync();
        var id = escenario.Busquedas[0];

        await using (var contexto = CrearContexto())
            await CrearRevisiones(contexto).CompartirAsync(id, escenario.ClienteId, escenario.MecanicoId);

        await using (var contexto = CrearContexto())
        {
            var baja = await CrearClientes(contexto).EliminarCuentaAsync(
                escenario.MecanicoId, new EliminarCuentaDto { Contrasena = Contrasena });
            Assert.True(baja.EsExitoso);
        }

        await using (var contexto = CrearContexto())
        {
            var busqueda = await contexto.Busquedas.SingleAsync(b => b.Id == id);
            Assert.Null(busqueda.MecanicoId);
        }
    }
}
