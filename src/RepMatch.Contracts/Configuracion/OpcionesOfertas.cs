using System.ComponentModel.DataAnnotations;

namespace RepMatch.Contracts.Configuracion;

/// <summary>
/// Configuracion externalizada de la busqueda de precios (seccion "Ofertas"). Todo se puede pisar
/// por variable de entorno, por ejemplo <c>Ofertas__Tiendas__0__UrlBase</c>: asi las pruebas de
/// punta a punta apuntan las tiendas a un simulador sin recompilar.
/// </summary>
public sealed class OpcionesOfertas
{
    public const string Seccion = "Ofertas";

    /// <summary>Tope para todas las tiendas juntas. Lo que llego antes se muestra; la tienda que no
    /// llego se avisa. Una tienda lenta no puede dejar a la persona esperando.</summary>
    [Range(1, 60)]
    public int TimeoutGlobalSegundos { get; set; } = 8;

    /// <summary>Despues de esto los precios se muestran como "de hace un rato" y se vuelven a pedir
    /// al abrir la consulta. Los precios de las tiendas cambian seguido.</summary>
    [Range(1, 10080)]
    public int VigenciaMinutos { get; set; } = 60;

    public List<OpcionesTiendaVtex> Tiendas { get; set; } = [];
}

/// <summary>
/// Una tienda montada sobre VTEX. Agregar otra tienda VTEX es agregar una entrada aca, sin codigo
/// nuevo (criterio de aceptacion de #18).
/// </summary>
public sealed class OpcionesTiendaVtex
{
    [Required]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Raiz del sitio, por ejemplo https://www.easy.com.ar.</summary>
    [Required]
    public string UrlBase { get; set; } = string.Empty;

    public bool Habilitada { get; set; } = true;

    /// <summary>VTEX no informa la moneda en la busqueda: la conoce quien configura la tienda.</summary>
    public string Moneda { get; set; } = "ARS";

    /// <summary>
    /// Solo se aceptan publicaciones de estas categorias. Sin este filtro, buscar "bateria" en Easy
    /// trae taladros y pilas, y "pastillas de freno" trae pastillas para encender el fuego.
    /// </summary>
    public List<string> PrefijosCategoria { get; set; } = [];

    [Range(1, 30)]
    public int TimeoutSegundos { get; set; } = 5;

    /// <summary>Cuantos pedidos simultaneos se le hacen a la tienda. Es una API publica y gratuita:
    /// no hay que saturarla.</summary>
    [Range(1, 10)]
    public int PedidosSimultaneos { get; set; } = 2;

    /// <summary>Tamanio de pagina (_from/_to). VTEX ordena por relevancia: la primera pagina alcanza.</summary>
    [Range(1, 50)]
    public int ResultadosPorTermino { get; set; } = 10;

    /// <summary>Cuantos minutos se reutiliza la respuesta de un mismo termino, para no repetir el pedido
    /// cuando varias personas buscan lo mismo.</summary>
    [Range(0, 1440)]
    public int CacheMinutos { get; set; } = 10;
}
