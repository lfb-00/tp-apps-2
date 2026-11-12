using RepMatch.Aplicacion.Ofertas;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Fuentes.Vtex;

namespace RepMatch.Tests.Ofertas;

public class ValidadorCandidatoOfertaTests
{
    private static readonly VehiculoDto Gol = new()
    {
        Marca = "Volkswagen",
        Modelo = "Gol",
        Anio = 2015,
        Motor = "1.6"
    };

    private static readonly RepuestoDto Pastillas = new()
    {
        Id = Guid.NewGuid(),
        CodigoCanonico = "PAST-VW-GOL-DEL",
        Nombre = "Pastillas de freno delanteras",
        Sistema = SistemaVehiculo.Frenos.ToString(),
        Aplicaciones =
        [
            new AplicacionDto
            {
                Marca = "Volkswagen",
                Modelo = "Gol",
                AnioDesde = 2009,
                AnioHasta = 2019
            }
        ]
    };

    private static readonly IReadOnlyList<OpcionVehiculoDto> Vehiculos =
    [
        new() { Marca = "Volkswagen", Modelo = "Gol", AnioDesde = 2009, AnioHasta = 2019, Motor = "1.6" },
        new() { Marca = "Toyota", Modelo = "Corolla", AnioDesde = 2014, AnioHasta = 2026, Motor = "1.8" }
    ];

    private static OfertaExternaDto Candidato(string titulo, params string[] atributos) => new()
    {
        CodigoRepuesto = Pastillas.CodigoCanonico,
        Tienda = "Frávega",
        Titulo = titulo,
        Precio = 1000,
        Moneda = "ARS",
        Url = "https://www.fravega.com/pastillas/p",
        Coincidencia = CoincidenciasOferta.ProbablePorNombre,
        Atributos = atributos
    };

    [Fact]
    public void Excluye_del_ranking_una_publicacion_de_Corolla_en_una_busqueda_de_Gol()
    {
        var motivo = ValidadorCandidatoOferta.MotivoRechazo(
            Candidato("Pastillas de freno Toyota Corolla delanteras"),
            Pastillas, Gol, Vehiculos);

        Assert.Equal("modelo distinto", motivo);
    }

    [Fact]
    public void Excluye_una_marca_distinta_cuando_el_modelo_coincide()
    {
        var vehiculos = Vehiculos.Append(
            new OpcionVehiculoDto { Marca = "Chevrolet", Modelo = "Gol", AnioDesde = 2010, AnioHasta = 2020, Motor = "1.6" });

        var motivo = ValidadorCandidatoOferta.MotivoRechazo(
            Candidato("Pastillas de freno Chevrolet Gol delanteras"),
            Pastillas, Gol, vehiculos.ToList());

        Assert.Equal("marca distinta", motivo);
    }

    [Fact]
    public void Excluye_una_publicacion_real_de_pastillas_traseras_al_buscar_delanteras()
    {
        var fravega = new OpcionesTiendaVtex
        {
            Nombre = "Frávega",
            UrlBase = "https://www.fravega.com",
            PrefijosCategoria = ["/Autos, Motos y Otros/"]
        };
        var consulta = new ConsultaOfertasDto
        {
            CodigoRepuesto = Pastillas.CodigoCanonico,
            Nombre = Pastillas.Nombre
        };
        var publicacion = Assert.Single(LectorVtex.Leer(
            LectorVtexTests.Grabada("fravega-pastillas-moto.json"),
            fravega,
            consulta,
            consulta.Nombre,
            esCodigo: false));

        var motivo = ValidadorCandidatoOferta.MotivoRechazo(publicacion, Pastillas, Gol, Vehiculos);

        Assert.Equal("posicion distinta", motivo);
    }

    [Fact]
    public void Rechaza_anios_fuera_del_rango_y_sistemas_incompatibles()
    {
        Assert.Equal(
            "anio fuera de rango",
            ValidadorCandidatoOferta.MotivoRechazo(
                Candidato("Pastillas de freno Volkswagen Gol 2010-2012"), Pastillas, Gol, Vehiculos));
        Assert.Equal(
            "sistema distinto",
            ValidadorCandidatoOferta.MotivoRechazo(
                Candidato("Filtro de aire Volkswagen Gol"), Pastillas, Gol, Vehiculos));
    }

    [Fact]
    public void Conserva_como_probable_la_publicacion_que_no_aporta_datos_incompatibles()
    {
        Assert.Null(ValidadorCandidatoOferta.MotivoRechazo(
            Candidato("Pastillas de freno delanteras"), Pastillas, Gol, Vehiculos));
    }
}
