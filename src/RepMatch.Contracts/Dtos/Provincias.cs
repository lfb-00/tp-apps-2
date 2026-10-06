namespace RepMatch.Contracts.Dtos;

/// <summary>Las 24 jurisdicciones de Argentina: 23 provincias y la Ciudad Autonoma de Buenos
/// Aires. Se guardan como texto en el perfil del cliente.</summary>
public static class Provincias
{
    public static readonly IReadOnlyList<string> Todas =
    [
        "Buenos Aires",
        "Catamarca",
        "Chaco",
        "Chubut",
        "Ciudad Autónoma de Buenos Aires",
        "Córdoba",
        "Corrientes",
        "Entre Ríos",
        "Formosa",
        "Jujuy",
        "La Pampa",
        "La Rioja",
        "Mendoza",
        "Misiones",
        "Neuquén",
        "Río Negro",
        "Salta",
        "San Juan",
        "San Luis",
        "Santa Cruz",
        "Santa Fe",
        "Santiago del Estero",
        "Tierra del Fuego, Antártida e Islas del Atlántico Sur",
        "Tucumán"
    ];
}
