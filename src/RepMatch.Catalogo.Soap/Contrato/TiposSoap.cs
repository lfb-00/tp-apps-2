using System.Runtime.Serialization;
using RepMatch.Contracts.Dtos;

namespace RepMatch.Catalogo.Soap.Contrato;

/// <summary>
/// Repuesto tal como viaja en los mensajes SOAP. Es un tipo propio y no el <see cref="RepuestoDto"/>
/// de Contracts a proposito: el WSDL es un contrato publico y tiene que quedar estable aunque los
/// DTO internos cambien. Usa arreglos porque es lo que el serializador de contratos describe sin
/// ambiguedad en el esquema XML.
/// </summary>
[DataContract(Namespace = Espacio.Nombre, Name = "Repuesto")]
public sealed class RepuestoSoap
{
    [DataMember(Order = 1, IsRequired = true)] public Guid Id { get; set; }
    [DataMember(Order = 2, IsRequired = true)] public string CodigoCanonico { get; set; } = string.Empty;
    [DataMember(Order = 3, IsRequired = true)] public string Nombre { get; set; } = string.Empty;
    [DataMember(Order = 4)] public string? Descripcion { get; set; }
    [DataMember(Order = 5, IsRequired = true)] public string Sistema { get; set; } = string.Empty;
    [DataMember(Order = 6)] public string[] CodigosEquivalentes { get; set; } = [];
    [DataMember(Order = 7)] public AplicacionSoap[] Aplicaciones { get; set; } = [];

    public static RepuestoSoap Desde(RepuestoDto dto) => new()
    {
        Id = dto.Id,
        CodigoCanonico = dto.CodigoCanonico,
        Nombre = dto.Nombre,
        Descripcion = dto.Descripcion,
        Sistema = dto.Sistema,
        CodigosEquivalentes = [.. dto.CodigosEquivalentes],
        Aplicaciones = [.. dto.Aplicaciones.Select(AplicacionSoap.Desde)]
    };
}

/// <summary>Un rango de vehiculos al que le va el repuesto. Motor null: todos los motores.</summary>
[DataContract(Namespace = Espacio.Nombre, Name = "Aplicacion")]
public sealed class AplicacionSoap
{
    [DataMember(Order = 1, IsRequired = true)] public string Marca { get; set; } = string.Empty;
    [DataMember(Order = 2, IsRequired = true)] public string Modelo { get; set; } = string.Empty;
    [DataMember(Order = 3, IsRequired = true)] public int AnioDesde { get; set; }
    [DataMember(Order = 4, IsRequired = true)] public int AnioHasta { get; set; }
    [DataMember(Order = 5)] public string? Motor { get; set; }

    public static AplicacionSoap Desde(AplicacionDto dto) => new()
    {
        Marca = dto.Marca,
        Modelo = dto.Modelo,
        AnioDesde = dto.AnioDesde,
        AnioHasta = dto.AnioHasta,
        Motor = dto.Motor
    };
}
