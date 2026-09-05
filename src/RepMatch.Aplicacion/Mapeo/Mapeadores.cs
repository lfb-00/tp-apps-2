using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Aplicacion.Mapeo;

/// <summary>
/// Traduccion entre entidades del dominio y DTOs de la frontera. Es manual a proposito: son
/// pocos tipos, el mapeo queda explicito y evita arrastrar una dependencia mas al proyecto.
/// </summary>
public static class Mapeadores
{
    public static DatosVehiculo AEntidad(this VehiculoDto dto) =>
        new(dto.Marca, dto.Modelo, dto.Anio, dto.Motor);

    public static VehiculoDto ADto(this DatosVehiculo datos) => new()
    {
        Marca = datos.Marca,
        Modelo = datos.Modelo,
        Anio = datos.Anio,
        Motor = datos.Motor
    };

    public static AplicacionDto ADto(this AplicacionVehiculo aplicacion) => new()
    {
        Marca = aplicacion.Marca,
        Modelo = aplicacion.Modelo,
        AnioDesde = aplicacion.AnioDesde,
        AnioHasta = aplicacion.AnioHasta,
        Motor = aplicacion.Motor
    };

    public static RepuestoDto ADto(this Repuesto repuesto) => new()
    {
        Id = repuesto.Id,
        CodigoCanonico = repuesto.CodigoCanonico,
        Nombre = repuesto.Nombre,
        Descripcion = repuesto.Descripcion,
        Sistema = repuesto.Sistema.ToString(),
        CodigosEquivalentes = [.. repuesto.CodigosEquivalentes],
        Aplicaciones = [.. repuesto.Aplicaciones.Select(a => a.ADto())]
    };

    public static ClienteDto ADto(this Cliente cliente) => new()
    {
        Id = cliente.Id,
        Nombre = cliente.Nombre,
        Email = cliente.Email,
        FechaAlta = cliente.FechaAlta,
        Vehiculos = [.. cliente.Vehiculos.Select(v => new VehiculoClienteDto
        {
            Id = v.Id,
            Alias = v.Alias,
            Datos = v.Datos.ADto(),
            Vin = v.Vin
        })]
    };

    public static OfertaDto ADto(this Oferta oferta) => new()
    {
        CodigoRepuesto = oferta.CodigoRepuesto,
        NombreTienda = oferta.NombreTienda,
        Titulo = oferta.Titulo,
        Precio = oferta.Precio.Monto,
        Moneda = oferta.Precio.Moneda,
        CostoEnvio = oferta.CostoEnvio?.Monto,
        PrecioTotal = oferta.PrecioTotal.Monto,
        UrlOriginal = oferta.UrlOriginal,
        Disponible = oferta.Disponible,
        CapturadaEn = oferta.CapturadaEn
    };

    public static BusquedaDto ADto(this Busqueda busqueda) => new()
    {
        Id = busqueda.Id,
        ClienteId = busqueda.ClienteId,
        Vehiculo = busqueda.Vehiculo.ADto(),
        TextoLibre = busqueda.TextoLibre,
        Estado = busqueda.Estado.ToString(),
        FechaCreacion = busqueda.FechaCreacion,
        CodigosObjetivo = [.. busqueda.CodigosObjetivo],
        Ofertas = [.. busqueda.Ofertas.Select(o => o.ADto())]
    };

    /// <summary>Convierte el nombre de un sistema a la enumeracion del dominio.</summary>
    public static SistemaVehiculo? ASistema(string? sistema)
    {
        if (string.IsNullOrWhiteSpace(sistema)) return null;

        return Enum.TryParse<SistemaVehiculo>(sistema, ignoreCase: true, out var valor)
            && valor != SistemaVehiculo.Desconocido
            ? valor
            : null;
    }
}
