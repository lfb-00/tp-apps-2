using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.IA;
using RepMatch.IA.Llm;
using RepMatch.IA.Local;

namespace RepMatch.Tests.IA;

/// <summary>
/// El adapter del LLM con respuestas grabadas, sin pegarle al proveedor: valida el esquema,
/// descarta codigos inventados y nunca manda datos de contacto. Y la conmutacion al respaldo.
/// </summary>
public class DiagnosticoLlmTests
{
    private static readonly OpcionesLlm ConClave = new() { ApiKey = "clave-de-prueba", UrlBase = "https://llm.test/v1/" };

    [Fact]
    public void Interpreta_una_respuesta_valida_y_descarta_codigos_inventados()
    {
        var diagnostico = DiagnosticadorLlm.Interpretar("""
            {"sistema": "Frenos", "urgencia": "Alta",
             "causas": [{"causa": "Discos alabeados", "confianza": 0.82}, {"causa": "Pastillas gastadas", "confianza": 1.7}],
             "codigos": ["disc-vw-gol-256", "PAST-INVENTADO-999", "PAST-VW-GOL-DEL"]}
            """, CandidatosGol.Todos);

        Assert.Equal("Frenos", diagnostico.Sistema);
        Assert.Equal("Alta", diagnostico.Urgencia);
        Assert.Equal(["DISC-VW-GOL-256", "PAST-VW-GOL-DEL"], diagnostico.Codigos);
        Assert.Equal(1.0, diagnostico.Causas[1].Confianza);
        Assert.Equal(DiagnosticadorLlm.NombreFuente, diagnostico.Fuente);
    }

    [Fact]
    public void Acepta_el_sistema_con_tildes_y_otra_capitalizacion()
    {
        var diagnostico = DiagnosticadorLlm.Interpretar(
            """{"sistema": "suspensión", "urgencia": "crítica", "causas": [], "codigos": []}""", CandidatosGol.Todos);

        Assert.Equal("Suspension", diagnostico.Sistema);
        Assert.Equal("Critica", diagnostico.Urgencia);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""["Frenos"]""")]
    [InlineData("""{"urgencia": "Alta", "codigos": []}""")]
    [InlineData("""{"sistema": "Turbo cuántico", "codigos": []}""")]
    public void Rechaza_lo_que_no_respeta_el_esquema(string contenido)
    {
        Assert.Throws<RespuestaLlmInvalidaException>(() => DiagnosticadorLlm.Interpretar(contenido, CandidatosGol.Todos));
    }

    [Fact]
    public async Task Llama_al_proveedor_con_la_clave_y_sin_datos_de_contacto()
    {
        var proveedor = new ProveedorGrabado(HttpStatusCode.OK, RespuestaChat(
            """{"sistema": "Frenos", "urgencia": "Media", "causas": [{"causa": "Pastillas gastadas", "confianza": 0.7}], "codigos": ["PAST-VW-GOL-DEL"]}"""));
        var llm = new DiagnosticadorLlm(
            new HttpClient(proveedor) { BaseAddress = new Uri(ConClave.UrlBase) }, ConClave, NullLogger<DiagnosticadorLlm>.Instance);

        var diagnostico = await llm.DiagnosticarAsync(
            CandidatosGol.Solicitud("ruido al frenar, llamame al 11 5555-1234 o a ana@ejemplo.com"));

        Assert.Equal(["PAST-VW-GOL-DEL"], diagnostico.Codigos);
        Assert.Equal("https://llm.test/v1/chat/completions", proveedor.Url);
        Assert.Equal("Bearer clave-de-prueba", proveedor.Autorizacion);
        Assert.DoesNotContain("5555-1234", proveedor.Cuerpo);
        Assert.DoesNotContain("ana@ejemplo.com", proveedor.Cuerpo);
        Assert.Contains("PAST-VW-GOL-DEL", proveedor.Cuerpo);
    }

    [Fact]
    public async Task Si_el_proveedor_falla_responde_el_clasificador_local()
    {
        var llm = new DiagnosticadorLlm(
            new HttpClient(new ProveedorGrabado(HttpStatusCode.TooManyRequests, "{}")) { BaseAddress = new Uri(ConClave.UrlBase) },
            ConClave, NullLogger<DiagnosticadorLlm>.Instance);

        var diagnostico = await ConRespaldo(llm, ConClave).DiagnosticarAsync(CandidatosGol.Solicitud("hace un ruido al frenar"));

        Assert.Equal(DiagnosticadorLocal.NombreFuente, diagnostico.Fuente);
        Assert.Equal("Frenos", diagnostico.Sistema);
    }

    [Fact]
    public async Task Si_el_proveedor_responde_basura_responde_el_clasificador_local()
    {
        var llm = new DiagnosticadorLlm(
            new HttpClient(new ProveedorGrabado(HttpStatusCode.OK, RespuestaChat("lo siento, no puedo ayudarte")))
            { BaseAddress = new Uri(ConClave.UrlBase) },
            ConClave, NullLogger<DiagnosticadorLlm>.Instance);

        var diagnostico = await ConRespaldo(llm, ConClave).DiagnosticarAsync(CandidatosGol.Solicitud("pierde agua"));

        Assert.Equal(DiagnosticadorLocal.NombreFuente, diagnostico.Fuente);
    }

    [Fact]
    public async Task Sin_clave_ni_siquiera_intenta_el_llm()
    {
        var llm = new DiagnosticadorQueNoDebeLlamarse();

        var diagnostico = await ConRespaldo(llm, new OpcionesLlm()).DiagnosticarAsync(CandidatosGol.Solicitud("pierde agua"));

        Assert.Equal(DiagnosticadorLocal.NombreFuente, diagnostico.Fuente);
        Assert.False(llm.FueLlamado);
    }

    private static DiagnosticadorConRespaldo ConRespaldo(IDiagnosticador llm, OpcionesLlm opciones) =>
        new(llm, new DiagnosticadorLocal(ClasificadorSintomas.CargarCorpusIncluido()), opciones,
            NullLogger<DiagnosticadorConRespaldo>.Instance);

    /// <summary>El sobre de chat completions de OpenAI, con el JSON del modelo como contenido.</summary>
    private static string RespuestaChat(string contenido) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { role = "assistant", content = contenido } } }
    });

    private sealed class ProveedorGrabado(HttpStatusCode estado, string respuesta) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Autorizacion { get; private set; }
        public string Cuerpo { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            Url = peticion.RequestUri?.ToString();
            Autorizacion = peticion.Headers.Authorization?.ToString();
            Cuerpo = peticion.Content is null ? string.Empty : await peticion.Content.ReadAsStringAsync(ct);

            return new HttpResponseMessage(estado) { Content = new StringContent(respuesta, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class DiagnosticadorQueNoDebeLlamarse : IDiagnosticador
    {
        public bool FueLlamado { get; private set; }
        public string Modo => "Local";

        public Task<DiagnosticoDto> DiagnosticarAsync(SolicitudDiagnosticoDto solicitud, CancellationToken ct = default)
        {
            FueLlamado = true;
            throw new InvalidOperationException("No deberia llamarse sin clave.");
        }
    }
}
