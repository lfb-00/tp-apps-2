using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Eventos;

/// <summary>
/// Un mecanico reviso el diagnostico de una busqueda. Lleva lo que propuso el sistema y lo que
/// recomendo el profesional: cada evento es un ejemplo etiquetado para validar la IA.
/// </summary>
public sealed record DiagnosticoValidado(
    Guid BusquedaId,
    Guid ClienteId,
    Guid MecanicoId,
    bool Confirmado,
    IReadOnlyList<string> CodigosPropuestos,
    IReadOnlyList<string> CodigosRecomendados) : IEventoDominio
{
    public DateTimeOffset OcurridoEn { get; } = DateTimeOffset.UtcNow;
}
