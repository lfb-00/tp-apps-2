using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Configuracion;

namespace RepMatch.Notificaciones.Worker.Tareas;

/// <summary>
/// Mantiene la copia de las casas de repuestos de OpenStreetMap. Al arrancar importa si la copia
/// falta o tiene mas de <see cref="OpcionesMapas.RefrescoDias"/> dias; despues vuelve a mirar cada
/// seis horas. Si Overpass esta saturado reintenta a los <see cref="OpcionesMapas.ReintentoMinutos"/>
/// minutos, y mientras tanto la Web sigue usando la copia anterior.
///
/// Vive en el worker y no en la Web porque es trabajo en segundo plano que no tiene que ver con
/// ninguna peticion: si la descarga tarda minutos, nadie espera.
/// </summary>
public sealed class ImportadorCasasRepuestos(
    IServiceScopeFactory alcances, OpcionesMapas opciones, ILogger<ImportadorCasasRepuestos> log) : BackgroundService
{
    private static readonly TimeSpan Revision = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        while (!detener.IsCancellationRequested)
        {
            var espera = Revision;
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                var importacion = alcance.ServiceProvider.GetRequiredService<ServicioImportacionCasas>();
                if (await importacion.HaceFaltaImportarAsync(detener))
                {
                    var resultado = await importacion.ImportarAsync(detener);
                    if (resultado.EsFallido)
                        espera = TimeSpan.FromMinutes(opciones.ReintentoMinutos);
                }
            }
            catch (Exception ex) when (!detener.IsCancellationRequested)
            {
                // Por ejemplo, la base todavia no tiene la tabla: se vuelve a probar mas tarde.
                log.LogWarning(ex, "No se pudo revisar la copia de casas de repuestos");
                espera = TimeSpan.FromMinutes(opciones.ReintentoMinutos);
            }

            try
            {
                await Task.Delay(espera, detener);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
