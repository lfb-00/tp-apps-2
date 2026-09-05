using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Tests.Dominio;

/// <summary>
/// Invariantes del componente de dominio. Se testean sin base de datos ni contenedores: el
/// dominio no depende de nada, y esa independencia es justamente lo que hace que sea un
/// componente reutilizable.
/// </summary>
public class DatosVehiculoTests
{
    [Fact]
    public void Normaliza_espacios_y_compara_por_valor()
    {
        var uno = new DatosVehiculo("  Volkswagen ", "Gol ", 2015, " 1.6 ");
        var otro = new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6");

        Assert.Equal(otro, uno);
        Assert.Equal("Volkswagen Gol 2015 (1.6)", uno.ToString());
    }

    [Fact]
    public void Trata_el_motor_vacio_como_ausente()
    {
        var vehiculo = new DatosVehiculo("Fiat", "Cronos", 2020, "   ");

        Assert.Null(vehiculo.Motor);
        Assert.Equal("Fiat Cronos 2020", vehiculo.ToString());
    }

    [Theory]
    [InlineData(1949)]
    [InlineData(2200)]
    public void Rechaza_anios_fuera_de_rango(int anio) =>
        Assert.Throws<ExcepcionDominio>(() => new DatosVehiculo("Ford", "Ka", anio));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechaza_marca_vacia(string marca) =>
        Assert.Throws<ExcepcionDominio>(() => new DatosVehiculo(marca, "Gol", 2015));
}

public class DineroTests
{
    [Fact]
    public void Redondea_a_dos_decimales_y_normaliza_la_moneda()
    {
        var precio = new Dinero(45000.456m, "ars");

        Assert.Equal(45000.46m, precio.Monto);
        Assert.Equal("ARS", precio.Moneda);
    }

    [Fact]
    public void Suma_importes_de_la_misma_moneda()
    {
        var total = Dinero.Pesos(45000m) + Dinero.Pesos(5000m);

        Assert.Equal(50000m, total.Monto);
        Assert.Equal("ARS", total.Moneda);
    }

    [Fact]
    public void Impide_sumar_monedas_distintas()
    {
        // El comparador mezcla ofertas en ARS (tiendas VTEX) y USD (eBay): sumarlas sin
        // cotizacion tiene que ser un error ruidoso, no un numero silenciosamente incorrecto.
        var error = Assert.Throws<ExcepcionDominio>(() => Dinero.Pesos(1000m) + Dinero.Dolares(10m));

        Assert.Contains("distinta moneda", error.Message);
    }

    [Fact]
    public void Impide_comparar_monedas_distintas() =>
        Assert.Throws<ExcepcionDominio>(() => Dinero.Pesos(1000m).EsMasBaratoQue(Dinero.Dolares(10m)));

    [Fact]
    public void Rechaza_montos_negativos() =>
        Assert.Throws<ExcepcionDominio>(() => new Dinero(-1m, "ARS"));
}

public class CompatibilidadTests
{
    private static readonly AplicacionVehiculo GolSinMotor =
        new("Volkswagen", "Gol", 2009, 2019);

    private static readonly AplicacionVehiculo GolSolo16 =
        new("Volkswagen", "Gol", 2009, 2019, "1.6");

    [Fact]
    public void Acepta_un_vehiculo_dentro_del_rango_de_anios() =>
        Assert.True(GolSinMotor.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2015)));

    [Fact]
    public void Acepta_los_extremos_del_rango()
    {
        Assert.True(GolSinMotor.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2009)));
        Assert.True(GolSinMotor.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2019)));
    }

    [Fact]
    public void Rechaza_un_vehiculo_fuera_del_rango() =>
        Assert.False(GolSinMotor.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2020)));

    [Fact]
    public void Ignora_mayusculas_en_marca_y_modelo() =>
        Assert.True(GolSinMotor.EsCompatibleCon(new DatosVehiculo("VOLKSWAGEN", "gol", 2015)));

    [Fact]
    public void Un_motor_nulo_en_la_aplicacion_funciona_como_comodin() =>
        Assert.True(GolSinMotor.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2015, "1.4")));

    [Fact]
    public void Un_motor_nulo_en_el_vehiculo_no_descarta_la_aplicacion() =>
        Assert.True(GolSolo16.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2015)));

    [Fact]
    public void Rechaza_cuando_el_motor_no_coincide() =>
        Assert.False(GolSolo16.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2015, "1.4")));

    [Fact]
    public void Rechaza_rangos_de_anios_invertidos() =>
        Assert.Throws<ExcepcionDominio>(() => new AplicacionVehiculo("Fiat", "Cronos", 2020, 2015));
}

public class ClienteTests
{
    [Fact]
    public void Normaliza_el_email_a_minusculas()
    {
        var cliente = new Cliente("Ana Diaz", "  ANA@Ejemplo.COM ");

        Assert.Equal("ana@ejemplo.com", cliente.Email);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("@dominio.com")]
    [InlineData("usuario@")]
    [InlineData("usuario@sinpunto")]
    public void Rechaza_emails_invalidos(string email) =>
        Assert.Throws<ExcepcionDominio>(() => new Cliente("Ana", email));

    [Fact]
    public void Agrega_vehiculos_al_garage()
    {
        var cliente = new Cliente("Ana Diaz", "ana@ejemplo.com");

        var vehiculo = cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019), alias: "El chico");

        Assert.Single(cliente.Vehiculos);
        Assert.Equal("El chico", vehiculo.Alias);
        Assert.Equal(cliente.Id, vehiculo.ClienteId);
    }

    [Fact]
    public void Usa_la_descripcion_del_vehiculo_como_alias_por_defecto()
    {
        var cliente = new Cliente("Ana Diaz", "ana@ejemplo.com");

        var vehiculo = cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019));

        Assert.Equal("Peugeot 208 2019", vehiculo.Alias);
    }

    [Fact]
    public void Rechaza_el_mismo_vehiculo_dos_veces()
    {
        var cliente = new Cliente("Ana Diaz", "ana@ejemplo.com");
        cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019));

        Assert.Throws<ExcepcionDominio>(() =>
            cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019)));
    }

    [Fact]
    public void Valida_el_formato_del_vin()
    {
        var cliente = new Cliente("Ana Diaz", "ana@ejemplo.com");
        var datos = new DatosVehiculo("Peugeot", "208", 2019);

        // 17 caracteres, sin I/O/Q: es lo que despues acepta la API de NHTSA vPIC.
        var vehiculo = cliente.AgregarVehiculo(datos, vin: "9bwzzz377vt004251");
        Assert.Equal("9BWZZZ377VT004251", vehiculo.Vin);

        var otro = new Cliente("Beto", "beto@ejemplo.com");
        Assert.Throws<ExcepcionDominio>(() => otro.AgregarVehiculo(datos, vin: "CORTO"));
        Assert.Throws<ExcepcionDominio>(() => otro.AgregarVehiculo(datos, vin: "9BWZZZ377VT00425I"));
    }
}

public class BusquedaTests
{
    private static Busqueda NuevaBusqueda() =>
        new(Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
            "cuando freno vibra el volante");

    [Fact]
    public void Nace_pendiente_y_registra_el_evento_de_dominio()
    {
        var busqueda = NuevaBusqueda();

        Assert.Equal(EstadoBusqueda.Pendiente, busqueda.Estado);

        var evento = Assert.Single(busqueda.EventosDominio);
        Assert.IsType<RepMatch.Domain.Eventos.BusquedaCreada>(evento);
    }

    [Fact]
    public void Rechaza_descripciones_demasiado_cortas() =>
        Assert.Throws<ExcepcionDominio>(() =>
            new Busqueda(Guid.NewGuid(), new DatosVehiculo("Fiat", "Cronos", 2020), "hum"));

    [Fact]
    public void Normaliza_y_deduplica_los_codigos_objetivo()
    {
        var busqueda = NuevaBusqueda();

        busqueda.AsignarCodigosObjetivo(["past-vw-gol-del", "PAST-VW-GOL-DEL", " disc-vw-gol-256 ", ""]);

        Assert.Equal(2, busqueda.CodigosObjetivo.Count);
        Assert.Contains("PAST-VW-GOL-DEL", busqueda.CodigosObjetivo);
        Assert.Contains("DISC-VW-GOL-256", busqueda.CodigosObjetivo);
        Assert.Equal(EstadoBusqueda.Diagnosticada, busqueda.Estado);
    }

    [Fact]
    public void Ordena_las_ofertas_por_precio_total_no_por_precio_de_lista()
    {
        var busqueda = NuevaBusqueda();

        // La barata tiene envio caro: si se ordenara por precio de lista ganaria indebidamente.
        var conEnvioCaro = new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "tienda-a", "Pastillas",
            Dinero.Pesos(40000m), "https://tienda-a.test/1", costoEnvio: Dinero.Pesos(15000m));

        var conEnvioGratis = new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "tienda-b", "Pastillas",
            Dinero.Pesos(45000m), "https://tienda-b.test/2");

        busqueda.RegistrarOfertas([conEnvioCaro, conEnvioGratis]);

        var ordenadas = busqueda.OfertasOrdenadasPorPrecio();

        Assert.Equal("tienda-b", ordenadas[0].NombreTienda);
        Assert.Equal(45000m, ordenadas[0].PrecioTotal.Monto);
        Assert.Equal(55000m, ordenadas[1].PrecioTotal.Monto);
    }

    [Fact]
    public void Excluye_del_ranking_las_ofertas_de_otra_moneda()
    {
        var busqueda = NuevaBusqueda();

        busqueda.RegistrarOfertas(
        [
            new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "easy", "Pastillas",
                Dinero.Pesos(45000m), "https://easy.test/1"),
            new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "ebay", "Brake pads",
                Dinero.Dolares(35m), "https://ebay.test/2")
        ]);

        Assert.Single(busqueda.OfertasOrdenadasPorPrecio("ARS"));
        Assert.Single(busqueda.OfertasOrdenadasPorPrecio("USD"));
    }

    [Fact]
    public void Excluye_del_ranking_las_ofertas_no_disponibles()
    {
        var busqueda = NuevaBusqueda();

        busqueda.RegistrarOfertas(
        [
            new Oferta(busqueda.Id, "PAST-VW-GOL-DEL", "easy", "Pastillas",
                Dinero.Pesos(45000m), "https://easy.test/1", disponible: false)
        ]);

        Assert.Empty(busqueda.OfertasOrdenadasPorPrecio());
    }

    [Fact]
    public void No_admite_ofertas_despues_de_fallar()
    {
        var busqueda = NuevaBusqueda();
        busqueda.Fallar("Ninguna tienda respondio.");

        Assert.Equal(EstadoBusqueda.Fallida, busqueda.Estado);
        Assert.Equal("Ninguna tienda respondio.", busqueda.MotivoFalla);

        Assert.Throws<ExcepcionDominio>(() => busqueda.RegistrarOfertas([]));
    }
}

public class RepuestoTests
{
    [Fact]
    public void Normaliza_codigos_a_mayusculas_y_deduplica_equivalencias()
    {
        var repuesto = new Repuesto("past-vw-gol-del", "Pastillas", SistemaVehiculo.Frenos);

        repuesto.AgregarEquivalencia("5u0698151");
        repuesto.AgregarEquivalencia("5U0698151");

        Assert.Equal("PAST-VW-GOL-DEL", repuesto.CodigoCanonico);
        Assert.Single(repuesto.CodigosEquivalentes);
    }

    [Fact]
    public void Rechaza_que_el_codigo_canonico_figure_como_equivalencia()
    {
        var repuesto = new Repuesto("PAST-VW-GOL-DEL", "Pastillas", SistemaVehiculo.Frenos);

        Assert.Throws<ExcepcionDominio>(() => repuesto.AgregarEquivalencia("past-vw-gol-del"));
    }

    [Fact]
    public void Rechaza_un_repuesto_sin_sistema_conocido() =>
        Assert.Throws<ExcepcionDominio>(() =>
            new Repuesto("X-1", "Algo", SistemaVehiculo.Desconocido));

    [Fact]
    public void Construye_los_terminos_de_busqueda_para_las_tiendas()
    {
        var repuesto = new Repuesto("PAST-VW-GOL-DEL", "Pastillas de freno", SistemaVehiculo.Frenos);
        repuesto.AgregarEquivalencia("5U0698151");

        var terminos = repuesto.TerminosDeBusqueda();

        Assert.Equal(["PAST-VW-GOL-DEL", "5U0698151", "Pastillas de freno"], terminos);
    }

    [Fact]
    public void Es_compatible_si_alguna_de_sus_aplicaciones_lo_es()
    {
        var repuesto = new Repuesto("BATE-12V-60AH", "Bateria", SistemaVehiculo.Electrico);
        repuesto.AgregarAplicacion(new AplicacionVehiculo("Volkswagen", "Gol", 2009, 2019));
        repuesto.AgregarAplicacion(new AplicacionVehiculo("Peugeot", "208", 2013, 2023));

        Assert.True(repuesto.EsCompatibleCon(new DatosVehiculo("Peugeot", "208", 2019)));
        Assert.False(repuesto.EsCompatibleCon(new DatosVehiculo("Toyota", "Corolla", 2018)));
    }
}
