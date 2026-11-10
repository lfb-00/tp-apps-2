using System.Diagnostics;
using RepMatch.Contracts.Dtos;
using RepMatch.IA.Local;
using RepMatch.IA.Texto;

namespace RepMatch.Tests.IA;

/// <summary>Piezas compatibles con un Gol 1.6, tal como las devuelve el catalogo semilla.</summary>
internal static class CandidatosGol
{
    public static readonly IReadOnlyList<CandidatoDiagnosticoDto> Todos =
    [
        new() { Codigo = "PAST-VW-GOL-DEL", Nombre = "Pastillas de freno delanteras", Sistema = "Frenos" },
        new() { Codigo = "DISC-VW-GOL-256", Nombre = "Discos de freno delanteros 256mm", Sistema = "Frenos" },
        new() { Codigo = "FILT-ACE-VW-16", Nombre = "Filtro de aceite motor 1.6", Sistema = "Motor" },
        new() { Codigo = "BUJI-VW-GOL-16", Nombre = "Juego de bujias", Sistema = "Motor" },
        new() { Codigo = "AMOR-VW-GOL-DEL", Nombre = "Amortiguador delantero", Sistema = "Suspension" },
        new() { Codigo = "BATE-12V-60AH", Nombre = "Bateria 12V 60Ah", Sistema = "Electrico" },
        new() { Codigo = "BOMB-AGUA-VW-16", Nombre = "Bomba de agua", Sistema = "Refrigeracion" }
    ];

    public static readonly VehiculoDto Vehiculo = new() { Marca = "Volkswagen", Modelo = "Gol", Anio = 2015, Motor = "1.6" };

    public static SolicitudDiagnosticoDto Solicitud(string texto) => new()
    {
        Texto = texto,
        Vehiculo = Vehiculo,
        Candidatos = Todos
    };
}

/// <summary>
/// El clasificador local es el respaldo obligatorio de la IA: tiene que acertar el sistema en los
/// sintomas tipicos, elegir piezas reales y responder rapido sin red.
/// </summary>
public class DiagnosticoLocalTests
{
    private static readonly DiagnosticadorLocal Local = new(ClasificadorSintomas.CargarCorpusIncluido());

    [Fact]
    public void El_corpus_embebido_se_carga()
    {
        Assert.True(ClasificadorSintomas.CargarCorpusIncluido().CantidadFrases >= 40);
    }

    [Theory]
    [InlineData("Ruido al frenar", "Frenos")]
    [InlineData("cuando freno vibra el volante y se escucha un chirrido", "Frenos")]
    [InlineData("Pierde agua", "Refrigeracion")]
    [InlineData("se calienta cuando estoy parado en el semáforo", "Refrigeracion")]
    [InlineData("tironea en las subidas y se ahoga", "Motor")]
    [InlineData("no arranca, hace clic clic", "Electrico")]
    [InlineData("golpea mucho la suspensión en los pozos", "Suspension")]
    [InlineData("cuesta pasar los cambios", "Transmision")]
    public async Task Reconoce_el_sistema_de_sintomas_tipicos(string texto, string sistema)
    {
        var diagnostico = await Local.DiagnosticarAsync(CandidatosGol.Solicitud(texto));

        Assert.Equal(sistema, diagnostico.Sistema);
        Assert.Equal(DiagnosticadorLocal.NombreFuente, diagnostico.Fuente);
        Assert.NotEmpty(diagnostico.Causas);
    }

    [Fact]
    public async Task Elige_la_pieza_que_corresponde_al_sintoma()
    {
        var diagnostico = await Local.DiagnosticarAsync(CandidatosGol.Solicitud("vibra el volante cuando freno"));

        Assert.Equal("DISC-VW-GOL-256", diagnostico.Codigos[0]);
    }

    [Theory]
    [InlineData("hace un ruido al frenar")]
    [InlineData("pierde agua y sube la temperatura")]
    [InlineData("no arranca a la mañana")]
    public async Task Solo_devuelve_codigos_de_los_candidatos(string texto)
    {
        var diagnostico = await Local.DiagnosticarAsync(CandidatosGol.Solicitud(texto));

        Assert.NotEmpty(diagnostico.Codigos);
        Assert.All(diagnostico.Codigos, c => Assert.Contains(CandidatosGol.Todos, x => x.Codigo == c));
    }

    [Fact]
    public async Task Un_texto_que_no_es_un_sintoma_queda_sin_diagnostico()
    {
        var diagnostico = await Local.DiagnosticarAsync(CandidatosGol.Solicitud("hola, quería saber el horario"));

        Assert.Equal("Desconocido", diagnostico.Sistema);
        Assert.Empty(diagnostico.Codigos);
    }

    [Fact]
    public async Task Sin_piezas_del_tipo_se_queda_con_las_del_sistema()
    {
        // El Gol semilla no tiene radiador: "se calienta parado" apunta al radiador, asi que
        // tienen que quedar las piezas de refrigeracion que si hay.
        var diagnostico = await Local.DiagnosticarAsync(CandidatosGol.Solicitud("se calienta parado en el tránsito"));

        Assert.Equal(["BOMB-AGUA-VW-16"], diagnostico.Codigos);
    }

    [Fact]
    public async Task Responde_en_menos_de_50_ms()
    {
        await Local.DiagnosticarAsync(CandidatosGol.Solicitud("calentamiento"));   // primera llamada fuera de la medicion

        var reloj = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
            await Local.DiagnosticarAsync(CandidatosGol.Solicitud("hace un ruido al frenar y vibra el volante"));
        reloj.Stop();

        Assert.True(reloj.ElapsedMilliseconds / 20.0 < 50, $"Promedio {reloj.ElapsedMilliseconds / 20.0} ms");
    }

    [Fact]
    public void Normaliza_tildes_mayusculas_y_palabras_vacias()
    {
        Assert.Equal(["freno", "chirrido"], NormalizadorTexto.Palabras("¡Cuando FRENO hace un chirrido!"));
        Assert.Equal(["fren", "suspension"[..4]], NormalizadorTexto.Raices("Frenando la Suspensión"));
    }

    [Theory]
    [InlineData("llamame al +54 9 11 1234-5678 por favor", "llamame al [telefono] por favor")]
    [InlineData("escribime a ana.perez@ejemplo.com.ar", "escribime a [email]")]
    [InlineData("Gol 2015 con 120000 km, ruido al frenar", "Gol 2015 con 120000 km, ruido al frenar")]
    public void Saca_mails_y_telefonos_pero_no_anios_ni_kilometros(string texto, string esperado)
    {
        Assert.Equal(esperado, FiltroDatosPersonales.Filtrar(texto));
    }
}
