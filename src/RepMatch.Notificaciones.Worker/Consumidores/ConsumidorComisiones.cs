using Microsoft.Extensions.DependencyInjection;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Mensajeria;

namespace RepMatch.Notificaciones.Worker.Consumidores;

/// <summary>
/// Cola <c>repmatch.comisiones</c>: cada <c>derivacion.tienda</c> suma una salida al registro de
/// comisiones de esa tienda en el mes. Es trabajo que no tiene por que hacer esperar a la persona
/// que hizo clic en "Ir a la tienda", y que no se puede perder si este proceso esta caido: el
/// mensaje espera en la cola durable hasta que vuelva.
/// </summary>
public sealed class ConsumidorComisiones(
    ConexionRabbitMq conexion,
    OpcionesMensajeria opciones,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorComisiones> log)
    : ConsumidorRabbitMq<DerivacionTiendaV1>(conexion, opciones, scopes, log)
{
    public static readonly DefinicionCola Definicion = TopologiaRabbitMq.ColaComisiones;

    protected override DefinicionCola Cola => Definicion;

    protected override Task ProcesarAsync(
        IServiceProvider servicios, MensajeIntegracion<DerivacionTiendaV1> mensaje, CancellationToken ct) =>
        servicios.GetRequiredService<ServicioComisiones>().RegistrarDerivacionAsync(mensaje, ct);
}
