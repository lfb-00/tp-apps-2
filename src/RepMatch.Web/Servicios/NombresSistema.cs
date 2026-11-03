namespace RepMatch.Web.Servicios;

/// <summary>
/// Sistemas del vehiculo con su etiqueta en pantalla. Los valores son los nombres de
/// SistemaVehiculo del dominio, que la Web no referencia: viajan como texto en los DTOs.
/// </summary>
public static class NombresSistema
{
    public static readonly IReadOnlyList<string> Todos =
    [
        "Frenos", "Suspension", "Motor", "Transmision", "Electrico",
        "Refrigeracion", "Escape", "Direccion", "Carroceria", "Neumaticos"
    ];

    public static string Etiqueta(string sistema) => sistema switch
    {
        "Suspension" => "Suspensión",
        "Transmision" => "Transmisión",
        "Electrico" => "Eléctrico",
        "Refrigeracion" => "Refrigeración",
        "Direccion" => "Dirección",
        "Carroceria" => "Carrocería",
        "Neumaticos" => "Neumáticos",
        "Desconocido" => "Sin sistema",
        _ => sistema
    };
}
