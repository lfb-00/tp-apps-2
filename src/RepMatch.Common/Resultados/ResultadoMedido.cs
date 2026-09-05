using System.Diagnostics;

namespace RepMatch.Common.Resultados;

/// <summary>
/// Un valor junto con cuanto tardo en obtenerse y por que camino. Es la pieza que hace
/// observable el ejercicio de acceso local vs. remoto del TP Inicial: la misma operacion
/// devuelve el mismo valor por los dos caminos, pero con latencias distintas y medidas.
/// </summary>
public sealed record ResultadoMedido<T>(T Valor, TimeSpan Duracion, string Origen)
{
    public double MilisegundosTranscurridos => Duracion.TotalMilliseconds;

    public override string ToString() =>
        $"origen={Origen} duracion={MilisegundosTranscurridos:N1}ms";
}

public static class Cronometro
{
    /// <summary>Ejecuta la operacion midiendo su duracion real de pared.</summary>
    public static async Task<ResultadoMedido<T>> MedirAsync<T>(
        string origen, Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        ArgumentNullException.ThrowIfNull(operacion);

        var reloj = Stopwatch.StartNew();
        var valor = await operacion(ct).ConfigureAwait(false);
        reloj.Stop();

        return new ResultadoMedido<T>(valor, reloj.Elapsed, origen);
    }
}
