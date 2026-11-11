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
        Telefono = cliente.Telefono,
        TieneWhatsApp = cliente.TieneWhatsApp,
        Provincia = cliente.Provincia,
        Localidad = cliente.Localidad,
        FotoUrl = FotoUrl(cliente),
        VehiculoPredeterminadoId = cliente.VehiculoPredeterminadoId,
        TemaPreferido = cliente.TemaPreferido,
        Vehiculos = [.. cliente.Vehiculos.Select(v => new VehiculoClienteDto
        {
            Id = v.Id,
            Alias = v.Alias,
            Datos = v.Datos.ADto(),
            Vin = v.Vin
        })],
        Rol = cliente.Rol.ToString(),
        NombreTaller = cliente.NombreTaller,
        Especialidades = [.. cliente.Especialidades.Select(e => e.ToString())],
        Plan = cliente.Plan?.ToString()
    };

    /// <summary>Solo para cuentas con rol mecanico: el plan siempre esta cargado.</summary>
    public static MecanicoDto AMecanicoDto(this Cliente mecanico) => new()
    {
        Id = mecanico.Id,
        Nombre = mecanico.Nombre,
        NombreTaller = mecanico.NombreTaller,
        Provincia = mecanico.Provincia,
        Localidad = mecanico.Localidad,
        Telefono = mecanico.Telefono,
        TieneWhatsApp = mecanico.TieneWhatsApp,
        FotoUrl = FotoUrl(mecanico),
        Especialidades = [.. mecanico.Especialidades.Select(e => e.ToString())],
        Plan = (mecanico.Plan ?? PlanMecanico.Gratuito).ToString()
    };

    public static OfertaDto ADto(this Oferta oferta) => new()
    {
        Id = oferta.Id,
        CodigoRepuesto = oferta.CodigoRepuesto,
        NombreTienda = oferta.NombreTienda,
        Titulo = oferta.Titulo,
        Precio = oferta.Precio.Monto,
        Moneda = oferta.Precio.Moneda,
        CostoEnvio = oferta.CostoEnvio?.Monto,
        PrecioTotal = oferta.PrecioTotal.Monto,
        UrlOriginal = oferta.UrlOriginal,
        Disponible = oferta.Disponible,
        CapturadaEn = oferta.CapturadaEn,
        PrecioLista = oferta.PrecioLista?.Monto,
        Coincidencia = oferta.Coincidencia == CoincidenciaOferta.PorCodigo
            ? CoincidenciasOferta.PorCodigo
            : CoincidenciasOferta.PorNombre,
        Marca = oferta.Marca,
        Ean = oferta.Ean,
        Vendedor = oferta.Vendedor
    };

    public static BusquedaDto ADto(this Busqueda busqueda) => new()
    {
        Id = busqueda.Id,
        ClienteId = busqueda.ClienteId,
        Vehiculo = busqueda.Vehiculo.ADto(),
        TextoLibre = busqueda.TextoLibre,
        Estado = busqueda.Estado.ToString(),
        FechaCreacion = busqueda.FechaCreacion,
        MotivoFalla = busqueda.MotivoFalla,
        CodigosObjetivo = [.. busqueda.CodigosObjetivo],
        Diagnostico = busqueda.TieneDiagnostico
            ? new DiagnosticoBusquedaDto
            {
                Sistema = (busqueda.SistemaDiagnosticado ?? SistemaVehiculo.Desconocido).ToString(),
                Urgencia = busqueda.Urgencia.ToString(),
                Causas = [.. busqueda.CausasProbables],
                Fuente = busqueda.FuenteDiagnostico!
            }
            : null,
        Ofertas = [.. busqueda.Ofertas.Select(o => o.ADto())],
        PreciosConsultadosEn = busqueda.PreciosConsultadosEn,
        MecanicoId = busqueda.MecanicoId,
        CompartidaEn = busqueda.CompartidaEn,
        Revision = busqueda.RevisadaEn is { } revisadaEn
            ? new RevisionDto
            {
                ConfirmaDiagnostico = busqueda.DiagnosticoConfirmado ?? false,
                CodigosRecomendados = [.. busqueda.CodigosRecomendados],
                Nota = busqueda.NotaMecanico,
                RevisadaEn = revisadaEn
            }
            : null
    };

    private static string? FotoUrl(Cliente cliente) =>
        cliente.FotoPerfil is { Length: > 0 } foto
            ? $"data:{cliente.FotoTipoContenido};base64,{Convert.ToBase64String(foto)}"
            : null;

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
