using FluentValidation;
using FluentValidation.Results;
using RepMatch.Common.Resultados;

namespace RepMatch.Common.Validacion;

/// <summary>
/// Puente entre FluentValidation y <see cref="ResultadoOperacion"/>: valida y, si algo falla,
/// devuelve un resultado con todos los mensajes en vez de lanzar una excepcion.
/// </summary>
public static class ExtensionesValidacion
{
    public static async Task<ResultadoOperacion> ValidarAsync<T>(
        this IValidator<T> validador, T instancia, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(validador);

        ValidationResult resultado = await validador.ValidateAsync(instancia, ct).ConfigureAwait(false);

        return resultado.IsValid
            ? ResultadoOperacion.Exito()
            : ResultadoOperacion.Falla(resultado.Errors.Select(e => e.ErrorMessage));
    }

    /// <summary>Valida y, si pasa, envuelve el valor ya validado en el resultado.</summary>
    public static async Task<ResultadoOperacion<T>> ValidarYDevolverAsync<T>(
        this IValidator<T> validador, T instancia, CancellationToken ct = default)
    {
        var resultado = await validador.ValidarAsync(instancia, ct).ConfigureAwait(false);

        return resultado.EsExitoso
            ? ResultadoOperacion<T>.Exito(instancia)
            : ResultadoOperacion<T>.Falla(resultado.Errores);
    }
}
