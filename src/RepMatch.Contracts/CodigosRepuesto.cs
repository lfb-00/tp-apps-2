namespace RepMatch.Contracts;

/// <summary>
/// El catalogo guarda los numeros de reposicion con la marca adelante ("MANN W 712/83",
/// "Bosch 0 986 221 060") para que se entienda de quien es cada numero. Las tiendas no: Autodo
/// publica la referencia "W712-83-003" y eBay escribe la marca de mil maneras ("MANN-FILTER",
/// "Mann Filter"). Para buscar y comparar se usa el numero solo. Vive en Contracts porque lo
/// usan todos los adapters de tiendas (VTEX y eBay) y es parte del formato de
/// <see cref="Dtos.ConsultaOfertasDto.CodigosEquivalentes"/>.
/// </summary>
public static class CodigosRepuesto
{
    /// <summary>
    /// Saca la marca si la primera palabra es solo letras (o guiones, como "Fras-le"), tiene al
    /// menos tres y el resto trae algun digito. El minimo de tres deja intactos los numeros que
    /// empiezan con letras sueltas, como "PU 9023 z" o "HU 7002 z" sin la marca.
    /// </summary>
    public static string SinMarca(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        var partes = codigo.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 2
               && partes[0].Count(char.IsLetter) >= 3
               && partes[0].All(c => char.IsLetter(c) || c == '-')
               && partes[1].Any(char.IsDigit)
            ? partes[1].Trim()
            : codigo.Trim();
    }

    /// <summary>
    /// El termino que se manda al buscador de la tienda: sin marca y sin espacios. El buscador de
    /// VTEX separa por espacios: "W 712/83" busca "W" y "712/83" por separado y trae cualquier
    /// filtro W; "W712/83" encuentra la publicacion. Barras y guiones se conservan.
    /// </summary>
    public static string ParaBuscar(string codigo) => SinMarca(codigo).Replace(" ", string.Empty, StringComparison.Ordinal);
}
