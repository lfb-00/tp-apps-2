using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Common.Resultados;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
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

    /// <summary>
    /// La salida a la tienda de quien compara sin cuenta. No hay busqueda ni oferta guardadas:
    /// queda la pieza, la tienda y el destino. El enlace lo firma la Web, asi que nadie puede usar
    /// este camino para redirigir a un sitio cualquiera.
    /// </summary>
    public async Task<ResultadoOperacion<string>> DerivarSinCuentaAsync(
        string codigoRepuesto, string tienda, string url, CancellationToken ct = default)
    {
        try
        {
            var derivacion = Derivacion.SinCuenta(codigoRepuesto, tienda, url);
            var pendientes = derivacion.EventosDominio.ToArray();

            await derivaciones.AgregarAsync(derivacion, ct);
            await unidadDeTrabajo.ConfirmarAsync(ct);
            derivacion.LimpiarEventos();
            await despachador.DespacharAsync(pendientes, ct);

            log.LogInformation(
                "Derivacion sin cuenta {DerivacionId} a {Tienda} por {Codigo}",
                derivacion.Id, derivacion.NombreTienda, derivacion.CodigoRepuesto);

            return ResultadoOperacion<string>.Exito(derivacion.UrlDestino);
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<string>.Falla(ex.Message);
        }
    }
}
