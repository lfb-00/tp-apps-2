using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Tests.Dominio;

/// <summary>
/// Roles cliente y mecanico, y las reglas del modelo de negocio que viven en el dominio: limite
/// del plan gratuito, revision del diagnostico y atribucion de la derivacion.
/// </summary>
public class RolesTests
{
    private static readonly DatosVehiculo Gol = new("Volkswagen", "Gol", 2015, "1.6");

    private static Busqueda BusquedaDiagnosticada(Guid clienteId)
    {
        var busqueda = new Busqueda(clienteId, Gol, "vibra el volante al frenar");
        busqueda.AsignarCodigosObjetivo(["PAST-VW-GOL-DEL", "AMOR-VW-GOL-DEL"]);
        busqueda.LimpiarEventos();
        return busqueda;
    }

    [Fact]
    public void Un_cliente_no_tiene_plan_ni_datos_de_taller()
    {
        var cliente = new Cliente("Ana", "ana@ejemplo.com");

        Assert.Equal(RolUsuario.Cliente, cliente.Rol);
        Assert.Null(cliente.Plan);
        Assert.False(cliente.PuedeRecibirBusqueda(0));
        Assert.Throws<ExcepcionDominio>(() => cliente.ActualizarTaller("Taller", [SistemaVehiculo.Frenos]));
        Assert.Throws<ExcepcionDominio>(() => cliente.CambiarPlan(PlanMecanico.Pro));
    }

    [Fact]
    public void El_plan_gratuito_limita_la_bandeja_y_el_pro_no()
    {
        var mecanico = new Cliente("Carla", "carla@ejemplo.com", RolUsuario.Mecanico);

        Assert.Equal(PlanMecanico.Gratuito, mecanico.Plan);
        Assert.True(mecanico.PuedeRecibirBusqueda(Cliente.MaximoPendientesPlanGratuito - 1));
        Assert.False(mecanico.PuedeRecibirBusqueda(Cliente.MaximoPendientesPlanGratuito));

        mecanico.CambiarPlan(PlanMecanico.Pro);

        Assert.Null(mecanico.LimiteBusquedasPendientes);
        Assert.True(mecanico.PuedeRecibirBusqueda(1000));
    }

    [Fact]
    public void Actualizar_taller_ordena_especialidades_y_rechaza_desconocido()
    {
        var mecanico = new Cliente("Carla", "carla@ejemplo.com", RolUsuario.Mecanico);

        mecanico.ActualizarTaller("  Motores Benitez ", [SistemaVehiculo.Motor, SistemaVehiculo.Frenos, SistemaVehiculo.Motor]);

        Assert.Equal("Motores Benitez", mecanico.NombreTaller);
        Assert.Equal([SistemaVehiculo.Frenos, SistemaVehiculo.Motor], mecanico.Especialidades);
        Assert.Throws<ExcepcionDominio>(() => mecanico.ActualizarTaller(null, [SistemaVehiculo.Desconocido]));
    }

    [Fact]
    public void Compartir_registra_el_evento_y_no_admite_compartirse_a_si_mismo()
    {
        var clienteId = Guid.NewGuid();
        var mecanicoId = Guid.NewGuid();
        var busqueda = BusquedaDiagnosticada(clienteId);

        Assert.Throws<ExcepcionDominio>(() => busqueda.CompartirCon(clienteId));

        busqueda.CompartirCon(mecanicoId);

        Assert.Equal(mecanicoId, busqueda.MecanicoId);
        var evento = Assert.IsType<BusquedaCompartida>(Assert.Single(busqueda.EventosDominio));
        Assert.Equal(mecanicoId, evento.MecanicoId);
    }

    [Fact]
    public void Confirmar_recomienda_lo_propuesto_y_el_evento_lleva_el_par_etiquetado()
    {
        var mecanicoId = Guid.NewGuid();
        var busqueda = BusquedaDiagnosticada(Guid.NewGuid());
        busqueda.CompartirCon(mecanicoId);
        busqueda.LimpiarEventos();

        busqueda.RegistrarRevision(mecanicoId, confirmaDiagnostico: true, ["IGNORADO"], "  Todo bien  ");

        Assert.True(busqueda.DiagnosticoConfirmado);
        Assert.Equal(busqueda.CodigosObjetivo, busqueda.CodigosRecomendados);
        Assert.Equal("Todo bien", busqueda.NotaMecanico);
        var evento = Assert.IsType<DiagnosticoValidado>(Assert.Single(busqueda.EventosDominio));
        Assert.True(evento.Confirmado);
        Assert.Equal(evento.CodigosPropuestos, evento.CodigosRecomendados);
    }

    [Fact]
    public void Corregir_exige_codigos_y_guarda_los_del_mecanico_aparte_de_los_propuestos()
    {
        var mecanicoId = Guid.NewGuid();
        var busqueda = BusquedaDiagnosticada(Guid.NewGuid());
        busqueda.CompartirCon(mecanicoId);

        Assert.Throws<ExcepcionDominio>(() => busqueda.RegistrarRevision(mecanicoId, false, [" "], null));

        busqueda.RegistrarRevision(mecanicoId, false, ["disc-vw-gol-256"], null);

        Assert.False(busqueda.DiagnosticoConfirmado);
        Assert.Equal(["DISC-VW-GOL-256"], busqueda.CodigosRecomendados);
        Assert.Equal(2, busqueda.CodigosObjetivo.Count);
    }

    [Fact]
    public void Solo_revisa_el_mecanico_asignado_y_una_sola_vez()
    {
        var mecanicoId = Guid.NewGuid();
        var busqueda = BusquedaDiagnosticada(Guid.NewGuid());

        Assert.Throws<ExcepcionDominio>(() => busqueda.RegistrarRevision(mecanicoId, true, [], null));

        busqueda.CompartirCon(mecanicoId);
        Assert.Throws<ExcepcionDominio>(() => busqueda.RegistrarRevision(Guid.NewGuid(), true, [], null));

        busqueda.RegistrarRevision(mecanicoId, true, [], null);
        Assert.Throws<ExcepcionDominio>(() => busqueda.RegistrarRevision(mecanicoId, true, [], null));
        Assert.Throws<ExcepcionDominio>(busqueda.DejarDeCompartir);
    }

    [Fact]
    public void Derivar_atribuye_al_mecanico_solo_lo_que_recomendo()
    {
        var mecanicoId = Guid.NewGuid();
        var busqueda = BusquedaDiagnosticada(Guid.NewGuid());
        var pastillas = new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "Tienda A", "Pastillas Gol",
            Dinero.Pesos(25000), "https://tienda-a.example/pastillas");
        var amortiguador = new Oferta(busqueda.Id, "AMOR-VW-GOL-DEL", "Tienda B", "Amortiguador Gol",
            Dinero.Pesos(90000), "https://tienda-b.example/amortiguador");
        busqueda.RegistrarOfertas([pastillas, amortiguador]);

        // Antes de la revision ninguna derivacion es del mecanico.
        Assert.Null(busqueda.Derivar(pastillas.Id).MecanicoId);

        busqueda.CompartirCon(mecanicoId);
        busqueda.RegistrarRevision(mecanicoId, false, ["PAST-VW-GOL-DEL"], null);

        var recomendada = busqueda.Derivar(pastillas.Id);
        var noRecomendada = busqueda.Derivar(amortiguador.Id);

        Assert.Equal(mecanicoId, recomendada.MecanicoId);
        Assert.Equal("https://tienda-a.example/pastillas", recomendada.UrlDestino);
        Assert.IsType<DerivacionATienda>(Assert.Single(recomendada.EventosDominio));
        Assert.Null(noRecomendada.MecanicoId);
        Assert.Throws<ExcepcionDominio>(() => busqueda.Derivar(Guid.NewGuid()));
    }
}
