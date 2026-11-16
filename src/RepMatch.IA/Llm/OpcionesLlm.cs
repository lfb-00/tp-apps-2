using System.ComponentModel.DataAnnotations;

namespace RepMatch.IA.Llm;

/// <summary>
/// Proveedor del LLM. Se habla el formato de chat completions compatible con OpenAI, que ofrecen
/// Groq (https://api.groq.com/openai/v1/) y Gemini (https://generativelanguage.googleapis.com/v1beta/openai/),
/// los dos con cupo gratuito: cambiar de proveedor es cambiar <see cref="UrlBase"/> y
/// <see cref="Modelo"/>, no codigo.
///
/// La clave llega solo por variable de entorno (IA__Llm__ApiKey) o por el .env de compose, que
/// esta en .gitignore. Sin clave el LLM queda deshabilitado y responde el clasificador local.
/// </summary>
public sealed class OpcionesLlm
{
    public const string Seccion = "IA:Llm";

    public string UrlBase { get; set; } = "https://api.groq.com/openai/v1/";

    /// <summary>
    /// Modelo del proveedor. Groq retiro llama-3.3-70b-versatile (respondia 404 y todo caia al
    /// respaldo local sin que se notara); gpt-oss-120b es el modelo general mas capaz del cupo
    /// gratuito. Los proveedores retiran modelos: por eso vive en configuracion y no en codigo.
    /// </summary>
    public string Modelo { get; set; } = "openai/gpt-oss-120b";

    /// <summary>
    /// reasoning_effort de la API compatible con OpenAI. gpt-oss "piensa" antes de responder y
    /// esos tokens cuentan para el cupo (8000 por minuto en el plan gratuito de Groq): con "low"
    /// cada diagnostico baja de ~850 a ~700 tokens y la respuesta no cambia. Vacio no se envia,
    /// para los modelos que no razonan y rechazarian el parametro.
    /// </summary>
    public string EsfuerzoRazonamiento { get; set; } = "low";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Pasado este tiempo se abandona el LLM y responde el respaldo local. Es el
    /// requerimiento de latencia de la IA: la busqueda no espera mas que esto al proveedor.</summary>
    [Range(1, 60)]
    public int TimeoutSegundos { get; set; } = 4;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiKey);
}
