using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Common.Resultados;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Registra la salida hacia la tienda de una oferta y devuelve a donde redirigir. Es el unico
/// punto que toca el ingreso de RepMatch (comision de afiliado) y a proposito no participa del
/// ranking: las ofertas se ordenan por precio total, nunca por lo que paga la tienda.
/// </summary>
public sealed class ServicioDerivaciones(
    IBusquedaRepository busquedas,
    IDerivacionRepository derivaciones,
    IUnitOfWork unidadDeTrabajo,
    IDespachadorEventos despachador,
    ILogger<ServicioDerivaciones> log)
{
    /// <summary>Devuelve la URL de la tienda. Hasta que la Segunda Parte traiga ofertas de APIs
    /// externas no hay ofertas guardadas, y esto responde que la oferta no existe.</summary>
    public async Task<ResultadoOperacion<string>> DerivarAsync(Guid ofertaId, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorOfertaAsync(ofertaId, ct);
        if (busqueda is null)
            return ResultadoOperacion<string>.Falla("La oferta ya no existe.");

        try
        {
            var derivacion = busqueda.Derivar(ofertaId);
            var pendientes = derivacion.EventosDominio.ToArray();

            await derivaciones.AgregarAsync(derivacion, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);
            derivacion.LimpiarEventos();
            await despachador.DespacharAsync(pendientes, ct);

            log.LogInformation(
                "Derivacion {DerivacionId} a {Tienda} desde la busqueda {BusquedaId} (mecanico {MecanicoId})",
                derivacion.Id, derivacion.NombreTienda, derivacion.BusquedaId, derivacion.MecanicoId);

            return ResultadoOperacion<string>.Exito(derivacion.UrlDestino);
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<string>.Falla(ex.Message);
        }
    }
}
