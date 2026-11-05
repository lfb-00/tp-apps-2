using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>Camino por el que la Web alcanza al componente de IA.</summary>
public enum ModoAccesoIA
{
    /// <summary>La biblioteca RepMatch.IA en el mismo proceso.</summary>
    Local = 0,

    /// <summary>HTTP contra RepMatch.IA.Api.</summary>
    Remoto = 1
}

/// <summary>
/// Configuracion externalizada del acceso a la IA, con la misma forma que
/// <see cref="OpcionesCatalogo"/>: se cambia con IA__Modo e IA__UrlBaseRemota sin recompilar.
/// La configuracion del LLM (proveedor, modelo, clave) vive en RepMatch.IA, porque solo le
/// importa a quien ejecuta el diagnostico.
/// </summary>
public sealed class OpcionesIA
{
    public const string Seccion = "IA";

    [Required]
    public ModoAccesoIA Modo { get; set; } = ModoAccesoIA.Local;

    /// <summary>URL base de IA.Api. Solo se usa cuando <see cref="Modo"/> es Remoto.</summary>
    public string UrlBaseRemota { get; set; } = "http://localhost:5083";

    /// <summary>Tiene que superar el timeout del LLM dentro de IA.Api, para que el respaldo local
    /// alcance a responder antes de que la Web corte la llamada.</summary>
    [Range(1, 120)]
    public int TimeoutSegundos { get; set; } = 10;
}
