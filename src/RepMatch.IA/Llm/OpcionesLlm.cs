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

    public string Modelo { get; set; } = "llama-3.3-70b-versatile";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Pasado este tiempo se abandona el LLM y responde el respaldo local. Es el
    /// requerimiento de latencia de la IA: la busqueda no espera mas que esto al proveedor.</summary>
    [Range(1, 60)]
    public int TimeoutSegundos { get; set; } = 4;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiKey);
}
