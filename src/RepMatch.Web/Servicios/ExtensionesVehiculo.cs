using RepMatch.Contracts.Dtos;

namespace RepMatch.Web.Servicios;

public static class ExtensionesVehiculo
{
    /// <summary>"El Gol — Volkswagen Gol 2015 (1.6)", o solo el alias si ya es la descripción.</summary>
    public static string Etiqueta(this VehiculoClienteDto vehiculo) =>
        vehiculo.Alias == vehiculo.Datos.ToString()
            ? vehiculo.Alias
            : $"{vehiculo.Alias} — {vehiculo.Datos}";
}
