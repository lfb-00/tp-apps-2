using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>Camino por el que la presentacion alcanza al componente de catalogo.</summary>
public enum ModoAccesoCatalogo
{
    /// <summary>Invocacion directa en proceso: referencia de proyecto, sin red de por medio.</summary>
    Local = 0,

    /// <summary>Invocacion remota por HTTP contra RepMatch.Catalogo.Api.</summary>
    Remoto = 1
}

/// <summary>
/// Configuracion externalizada del acceso al catalogo. Se cambia por appsettings o por variable
/// de entorno (Catalogo__Modo=Remoto), sin recompilar: eso es lo que permite mostrar los dos
/// modos de ejecucion uno detras del otro durante la demo.
/// </summary>
public sealed class OpcionesCatalogo
{
    public const string Seccion = "Catalogo";

    [Required]
    public ModoAccesoCatalogo Modo { get; set; } = ModoAccesoCatalogo.Local;

    /// <summary>URL base de Catalogo.Api. Solo se usa cuando <see cref="Modo"/> es Remoto.</summary>
    public string UrlBaseRemota { get; set; } = "http://localhost:5081";

    [Range(1, 120)]
    public int TimeoutSegundos { get; set; } = 30;
}
