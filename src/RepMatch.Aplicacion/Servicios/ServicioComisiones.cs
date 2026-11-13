using Microsoft.Extensions.Logging;
using RepMatch.Contracts.Dtos;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Registro de comisiones por tienda, alimentado por la cola (issue #14). Es idempotente por
/// <c>MensajeId</c>: la marca de procesado y la suma se confirman juntas, asi que un mensaje que
/// llega dos veces cuenta una sola.
/// </summary>
public sealed class ServicioComisiones(
    IComisionesRepository comisiones,
    IUnitOfWork unidadDeTrabajo,
    ILogger<ServicioComisiones> log)
{
    public const string Consumidor = "comisiones";

    /// <summary>true si la sumo; false si ese mensaje ya se habia procesado.</summary>
    public async Task<bool> RegistrarDerivacionAsync(
        MensajeIntegracion<DerivacionTiendaV1> mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        if (await comisiones.YaProcesadoAsync(mensaje.MensajeId, Consumidor, ct))
        {
            log.LogInformation("Derivacion {MensajeId} ya registrada: se ignora la segunda entrega", mensaje.MensajeId);
            return false;
        }

        var datos = mensaje.Datos;
        var periodo = ComisionesTienda.PeriodoDe(mensaje.OcurridoEn);
        var fila = await comisiones.ObtenerAsync(datos.Tienda, periodo, ct);
        if (fila is null)
        {
            fila = new ComisionesTienda(datos.Tienda, periodo);
            await comisiones.AgregarAsync(fila, ct);
        }

        fila.Registrar(atribuidaAMecanico: datos.MecanicoId is not null, mensaje.OcurridoEn);
        await comisiones.MarcarProcesadoAsync(new MensajeProcesado(mensaje.MensajeId, Consumidor), ct);
        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation(
            "Derivacion a {Tienda} sumada al periodo {Periodo}: {Total} en el mes ({Atribuidas} por mecanicos)",
            fila.Tienda, fila.Periodo, fila.Derivaciones, fila.AtribuidasAMecanico);
        return true;
    }

    public async Task<IReadOnlyList<ComisionesTiendaDto>> ListarAsync(CancellationToken ct = default) =>
        [.. (await comisiones.ListarAsync(ct)).Select(c => new ComisionesTiendaDto
        {
            Tienda = c.Tienda,
            Periodo = c.Periodo,
            Derivaciones = c.Derivaciones,
            AtribuidasAMecanico = c.AtribuidasAMecanico,
            UltimaEn = c.UltimaEn
        })];
}
