using RepMatch.Domain.Entidades;

namespace RepMatch.Domain.Repositorios;

public interface ICasaRepuestosRepository
{
    /// <summary>Las que caen en el rectangulo dado (un primer filtro por indice; la distancia
    /// exacta la calcula quien llama). Lectura sin seguimiento.</summary>
    Task<IReadOnlyList<CasaRepuestos>> EnRectanguloAsync(
        double latitudMinima, double latitudMaxima, double longitudMinima, double longitudMaxima,
        CancellationToken ct = default);

    Task<int> ContarAsync(CancellationToken ct = default);

    /// <summary>Cuando se importaron las que hay. null si la tabla esta vacia.</summary>
    Task<DateTimeOffset?> UltimaImportacionAsync(CancellationToken ct = default);

    /// <summary>Borra las anteriores y agrega estas, en la unidad de trabajo: una importacion
    /// fallida a mitad de camino no deja la tabla vacia.</summary>
    Task ReemplazarTodasAsync(IReadOnlyCollection<CasaRepuestos> casas, CancellationToken ct = default);
}
