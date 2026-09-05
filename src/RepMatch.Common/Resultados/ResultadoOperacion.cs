namespace RepMatch.Common.Resultados;

/// <summary>
/// Resultado explicito de una operacion. Se usa para los errores esperables (validacion, no
/// encontrado, tienda caida) en vez de excepciones: las excepciones quedan reservadas para lo
/// verdaderamente excepcional, y la capa de presentacion puede mostrar el error sin try/catch.
/// </summary>
public class ResultadoOperacion
{
    private static readonly IReadOnlyList<string> SinErrores = [];

    protected ResultadoOperacion(bool esExitoso, IReadOnlyList<string> errores)
    {
        if (esExitoso && errores.Count > 0)
            throw new InvalidOperationException("Un resultado exitoso no puede tener errores.");
        if (!esExitoso && errores.Count == 0)
            throw new InvalidOperationException("Un resultado fallido debe explicar por que fallo.");

        EsExitoso = esExitoso;
        Errores = errores;
    }

    public bool EsExitoso { get; }
    public bool EsFallido => !EsExitoso;
    public IReadOnlyList<string> Errores { get; }

    /// <summary>Primer error, para los casos en que alcanza con mostrar uno solo.</summary>
    public string? Error => Errores.Count > 0 ? Errores[0] : null;

    public static ResultadoOperacion Exito() => new(true, SinErrores);

    public static ResultadoOperacion Falla(string error) => new(false, [error]);

    public static ResultadoOperacion Falla(IEnumerable<string> errores) =>
        new(false, [.. errores]);
}

/// <summary>Resultado que ademas transporta un valor cuando la operacion salio bien.</summary>
public sealed class ResultadoOperacion<T> : ResultadoOperacion
{
    private readonly T? _valor;

    private ResultadoOperacion(bool esExitoso, T? valor, IReadOnlyList<string> errores)
        : base(esExitoso, errores) => _valor = valor;

    /// <summary>Valor producido. Lanza si se accede sobre un resultado fallido: leer el valor de
    /// algo que fallo siempre es un bug del llamador.</summary>
    public T Valor => EsExitoso
        ? _valor!
        : throw new InvalidOperationException(
            $"No se puede leer el valor de un resultado fallido. Errores: {string.Join("; ", Errores)}");

    public static ResultadoOperacion<T> Exito(T valor) => new(true, valor, []);

    public static new ResultadoOperacion<T> Falla(string error) => new(false, default, [error]);

    public static new ResultadoOperacion<T> Falla(IEnumerable<string> errores) =>
        new(false, default, [.. errores]);
}
