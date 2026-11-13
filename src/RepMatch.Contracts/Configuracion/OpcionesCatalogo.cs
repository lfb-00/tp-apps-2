using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>Camino por el que la presentacion alcanza al componente de catalogo.</summary>
public enum ModoAccesoCatalogo
{
    /// <summary>Invocacion directa en proceso: referencia de proyecto, sin red de por medio.</summary>
    Local = 0,

    /// <summary>Invocacion remota por HTTP contra RepMatch.Catalogo.Api.</summary>
    Remoto = 1,

    /// <summary>
    /// Repuestos por SOAP contra RepMatch.Catalogo.Soap (Segunda Parte); las opciones de vehiculo
    /// siguen por REST contra Catalogo.Api. Asi una busqueda usa los dos protocolos que pide la
    /// consigna, y es el modo con que arranca la Web en docker compose.
    /// </summary>
    Soap = 2
}

/// <summary>
/// Configuracion externalizada del acceso al catalogo. Se cambia por appsettings o por variable
/// de entorno (Catalogo__Modo=Remoto o Soap), sin recompilar: eso es lo que permite mostrar los
/// tres modos de ejecucion uno detras del otro durante la demo.
/// </summary>
public sealed class OpcionesCatalogo
{
    public const string Seccion = "Catalogo";

    [Required]
    public ModoAccesoCatalogo Modo { get; set; } = ModoAccesoCatalogo.Local;

    /// <summary>URL base de Catalogo.Api. Se usa en modo Remoto y, para los vehiculos, en modo Soap.</summary>
    public string UrlBaseRemota { get; set; } = "http://localhost:5081";

    /// <summary>Direccion del servicio SOAP (el endpoint, no el WSDL). Solo se usa en modo Soap.</summary>
    public string UrlSoap { get; set; } = "http://localhost:5082/ConsultaCompatibilidad.svc";

    [Range(1, 120)]
    public int TimeoutSegundos { get; set; } = 30;
}
