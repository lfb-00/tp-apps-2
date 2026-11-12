using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepMatch.Aplicacion.Mapeo;
using RepMatch.Aplicacion.Ofertas;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Servicio de Ofertas: el "Servicio de Inventario" de la consigna (issue #4). RepMatch no tiene
/// stock propio, asi que su inventario son las ofertas de las tiendas: este servicio las pide,
/// las guarda en la busqueda sin duplicar, marca las que vencieron o desaparecieron y deja el
/// ranking al dominio (<see cref="Busqueda.OfertasOrdenadasPorPrecio"/>: solo precio, una moneda).
///
/// Corre aparte de la creacion de la busqueda a proposito. La persona ve primero el diagnostico y
/// las piezas, y los precios llegan despues: una tienda lenta no demora el diagnostico. Hoy lo
/// dispara la pantalla apenas muestra las piezas; con la cola de mensajes (issues #13 y #14) lo
/// dispara el consumidor de BusquedaCreada y DiagnosticoValidado, con el mismo metodo y sin tocar
/// la pantalla, que ya sabe mostrar "Buscando precios…" mientras tanto.
///
/// Reglas de uso real:
/// <list type="bullet">
/// <item>Todas las tiendas se consultan en paralelo con un tope global. Lo que llego, vale.</item>
/// <item>Una tienda caida no tumba a las demas: se informa en <c>TiendasSinRespuesta</c>.</item>
/// <item>Las ofertas de una tienda que no respondio no se marcan como agotadas.</item>
/// <item>Tambien se buscan precios de las piezas que recomendo el mecanico al corregir.</item>
/// </list>
/// </summary>
public sealed class ServicioOfertas(
    IEnumerable<IFuenteOfertas> fuentes,
    IBusquedaRepository busquedas,
    ICatalogoRepuestos catalogo,
    ICatalogoVehiculos catalogoVehiculos,
    IUnitOfWork unidadDeTrabajo,
    OpcionesOfertas opciones,
    ILogger<ServicioOfertas> log)
{
    private readonly IReadOnlyList<IFuenteOfertas> _fuentes = [.. fuentes];

    private TimeSpan Vigencia => TimeSpan.FromMinutes(opciones.VigenciaMinutos);

    /// <summary>
    /// Precios de una busqueda guardada. Consulta las tiendas solo para las piezas que lo necesitan
    /// (nunca consultadas o vencidas); con <paramref name="forzar"/> vuelve a preguntar por todas.
    /// Solo lo pueden pedir el duenio y el mecanico con quien esta compartida.
    /// </summary>
    public async Task<ResultadoOperacion<OfertasBusquedaDto>> ObtenerAsync(
        Guid busquedaId, Guid usuarioId, bool forzar = false, CancellationToken ct = default)
    {
        var busqueda = await busquedas.ObtenerPorIdAsync(busquedaId, ct);
        if (busqueda is null || (busqueda.ClienteId != usuarioId && busqueda.MecanicoId != usuarioId))
            return ResultadoOperacion<OfertasBusquedaDto>.Falla("La consulta no existe o no es tuya.");

        if (busqueda.Estado is EstadoBusqueda.Fallida or EstadoBusqueda.Pendiente or EstadoBusqueda.Borrador)
            return ResultadoOperacion<OfertasBusquedaDto>.Exito(new OfertasBusquedaDto { Busqueda = busqueda.ADto() });

        var codigos = forzar
            ? busqueda.CodigosQueNecesitanPrecios(TimeSpan.Zero)
            : busqueda.CodigosQueNecesitanPrecios(Vigencia);

        if (codigos.Count == 0 || _fuentes.Count == 0)
            return ResultadoOperacion<OfertasBusquedaDto>.Exito(new OfertasBusquedaDto { Busqueda = busqueda.ADto() });

        IReadOnlyList<RepuestoDto> piezas;
        try
        {
            piezas = await PiezasAsync(busqueda.Vehiculo.ADto(), codigos, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Sin catalogo no se pueden buscar precios de la busqueda {BusquedaId}", busquedaId);
            return ResultadoOperacion<OfertasBusquedaDto>.Falla("No se pudo consultar el catálogo. Probá de nuevo en unos segundos.");
        }

        var consulta = await ConsultarTiendasAsync(piezas, busqueda.Vehiculo.ADto(), ct);

        var capturadas = new List<Oferta>();
        foreach (var externa in consulta.Ofertas)
        {
            if (AOferta(busqueda.Id, externa) is { } oferta)
                capturadas.Add(oferta);
        }

        try
        {
            busqueda.ActualizarOfertas(codigos, consulta.Respondieron, capturadas);
        }
        catch (ExcepcionDominio ex)
        {
            return ResultadoOperacion<OfertasBusquedaDto>.Falla(ex.Message);
        }

        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation(
            "Precios de la busqueda {BusquedaId}: {Ofertas} ofertas de {Piezas} piezas; sin respuesta: {SinRespuesta}",
            busqueda.Id, capturadas.Count, codigos.Count,
            consulta.SinRespuesta.Count == 0 ? "ninguna" : string.Join(", ", consulta.SinRespuesta));

        return ResultadoOperacion<OfertasBusquedaDto>.Exito(new OfertasBusquedaDto
        {
            Busqueda = busqueda.ADto(),
            TiendasSinRespuesta = consulta.SinRespuesta
        });
    }

    /// <summary>
    /// Para quien busca sin cuenta: precio mas bajo y cantidad de tiendas por pieza, sin guardar
    /// nada y sin decir donde. Ver el detalle y derivar requiere ingresar, porque la derivacion
    /// sale de una busqueda guardada.
    /// </summary>
    public async Task<ResumenPreciosBusquedaDto> ResumirAsync(
        IReadOnlyList<RepuestoDto> piezas, VehiculoDto vehiculo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(piezas);
        ArgumentNullException.ThrowIfNull(vehiculo);
        if (piezas.Count == 0 || _fuentes.Count == 0)
            return new ResumenPreciosBusquedaDto();

        var consulta = await ConsultarTiendasAsync(piezas, vehiculo, ct);

        var porPieza = piezas.Select(p =>
        {
            var deLaPieza = consulta.Ofertas
                .Where(o => o.CodigoRepuesto.Equals(p.CodigoCanonico, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Una sola moneda por resumen, como en el ranking: ARS si hay, si no la que haya.
            var moneda = deLaPieza.Any(o => o.Moneda == "ARS") ? "ARS" : deLaPieza.FirstOrDefault()?.Moneda ?? "ARS";
            var enMoneda = deLaPieza.Where(o => o.Moneda == moneda).ToList();

            return new ResumenPreciosDto
            {
                CodigoRepuesto = p.CodigoCanonico,
                Desde = enMoneda.Count > 0 ? enMoneda.Min(o => o.Precio) : null,
                Moneda = moneda,
                Tiendas = enMoneda.Select(o => o.Tienda).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            };
        }).ToList();

        return new ResumenPreciosBusquedaDto { Piezas = porPieza, TiendasSinRespuesta = consulta.SinRespuesta };
    }

    /// <summary>Datos de catalogo de las piezas a buscar: nombre y codigos equivalentes.</summary>
    private async Task<IReadOnlyList<RepuestoDto>> PiezasAsync(
        VehiculoDto vehiculo, IReadOnlyList<string> codigos, CancellationToken ct)
    {
        var compatibles = await catalogo.BuscarCompatiblesAsync(vehiculo, sistema: null, ct);
        var porCodigo = compatibles.ToDictionary(r => r.CodigoCanonico, StringComparer.OrdinalIgnoreCase);

        var piezas = new List<RepuestoDto>();
        foreach (var codigo in codigos)
        {
            var pieza = porCodigo.GetValueOrDefault(codigo) ?? await catalogo.ObtenerPorCodigoAsync(codigo, ct);
            if (pieza is not null)
                piezas.Add(pieza);
        }

        return piezas;
    }

    private sealed record ResultadoTiendas(
        IReadOnlyList<OfertaExternaDto> Ofertas,
        IReadOnlyList<string> Respondieron,
        IReadOnlyList<string> SinRespuesta);

    /// <summary>
    /// Pregunta a cada tienda por cada pieza, todo en paralelo y con un tope global. Cada tienda
    /// limita sus propios pedidos simultaneos. Una tienda "respondio" si contesto por todas las
    /// piezas: si fallo en alguna, sus ofertas viejas no se tocan.
    /// </summary>
    private async Task<ResultadoTiendas> ConsultarTiendasAsync(
        IReadOnlyList<RepuestoDto> piezas, VehiculoDto vehiculo, CancellationToken ct)
    {
        var vehiculosConocidos = await VehiculosConocidosAsync(ct);
        using var tope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tope.CancelAfter(TimeSpan.FromSeconds(opciones.TimeoutGlobalSegundos));

        var tareas = _fuentes
            .SelectMany(fuente => piezas.Select(pieza => ConsultarAsync(fuente, pieza, tope.Token)))
            .ToList();

        var resultados = await Task.WhenAll(tareas);
        ct.ThrowIfCancellationRequested();

        var fallidas = resultados.Where(r => !r.Respondio).Select(r => r.Tienda)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var respondieron = _fuentes.Select(f => f.Nombre)
            .Where(n => !fallidas.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();

        var ofertas = ClasificarPorEan([.. resultados.SelectMany(r => r.Ofertas)]);
        var piezasPorCodigo = piezas.ToDictionary(p => p.CodigoCanonico, StringComparer.OrdinalIgnoreCase);
        var candidatasPorNombre = 0;
        var descartadas = 0;
        var motivos = new Dictionary<string, int>(StringComparer.Ordinal);
        var aceptadas = new List<OfertaExternaDto>(ofertas.Count);

        foreach (var oferta in ofertas)
        {
            if (oferta.Coincidencia == CoincidenciasOferta.ProbablePorNombre)
                candidatasPorNombre++;

            if (piezasPorCodigo.TryGetValue(oferta.CodigoRepuesto, out var pieza)
                && ValidadorCandidatoOferta.MotivoRechazo(oferta, pieza, vehiculo, vehiculosConocidos) is { } motivo)
            {
                descartadas++;
                motivos[motivo] = motivos.GetValueOrDefault(motivo) + 1;
                continue;
            }

            aceptadas.Add(oferta);
        }

        if (candidatasPorNombre > 0)
        {
            log.LogInformation(
                "Validacion de candidatos por nombre: vehiculo={Vehiculo}, candidatas={Candidatas}, descartadas={Descartadas}, tasaDescarte={TasaDescarte:P1}, motivos={Motivos}",
                vehiculo, candidatasPorNombre, descartadas,
                (double)descartadas / candidatasPorNombre,
                motivos.Count == 0 ? "ninguno" : string.Join(", ", motivos.Select(m => $"{m.Key}:{m.Value}")));
        }

        return new ResultadoTiendas(aceptadas, respondieron, fallidas);
    }

    private async Task<IReadOnlyList<OpcionVehiculoDto>> VehiculosConocidosAsync(CancellationToken ct)
    {
        try
        {
            return await catalogoVehiculos.ListarOpcionesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "No se pudo consultar el catalogo vehicular para validar candidatos por nombre");
            return [];
        }
    }

    private static IReadOnlyList<OfertaExternaDto> ClasificarPorEan(IReadOnlyList<OfertaExternaDto> ofertas)
    {
        var eanCompartidos = ofertas
            .Where(o => !string.IsNullOrWhiteSpace(o.Ean))
            .GroupBy(o => o.Ean!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(o => o.Tienda).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. ofertas.Select(o => eanCompartidos.Contains(o.Ean ?? string.Empty)
            ? o with { Coincidencia = CoincidenciasOferta.ExactoPorEan }
            : o)];
    }

    private async Task<(string Tienda, bool Respondio, IReadOnlyList<OfertaExternaDto> Ofertas)> ConsultarAsync(
        IFuenteOfertas fuente, RepuestoDto pieza, CancellationToken ct)
    {
        try
        {
            var ofertas = await fuente.BuscarAsync(new ConsultaOfertasDto
            {
                CodigoRepuesto = pieza.CodigoCanonico,
                Nombre = pieza.Nombre,
                CodigosEquivalentes = pieza.CodigosEquivalentes
            }, ct);
            return (fuente.Nombre, true, ofertas);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            log.LogWarning(
                "Tienda {Tienda} sin respuesta para {Codigo}: {Motivo}",
                fuente.Nombre, pieza.CodigoCanonico, ex is OperationCanceledException ? "se agoto el tiempo" : ex.Message);
            return (fuente.Nombre, false, []);
        }
    }

    /// <summary>Una oferta externa mal formada (moneda o URL invalida) se descarta sola, sin
    /// perder las demas.</summary>
    private Oferta? AOferta(Guid busquedaId, OfertaExternaDto externa)
    {
        try
        {
            return new Oferta(
                busquedaId,
                externa.CodigoRepuesto,
                externa.Tienda,
                externa.Titulo,
                new Dinero(externa.Precio, externa.Moneda),
                externa.Url,
                precioLista: externa.PrecioLista is { } lista ? new Dinero(lista, externa.Moneda) : null,
                coincidencia: externa.Coincidencia switch
                {
                    CoincidenciasOferta.ExactoPorEan => CoincidenciaOferta.ExactoPorEan,
                    CoincidenciasOferta.ExactoPorCodigo => CoincidenciaOferta.ExactoPorCodigo,
                    _ => CoincidenciaOferta.ProbablePorNombre
                },
                marca: externa.Marca,
                ean: externa.Ean,
                referenciaTienda: externa.Referencia,
                vendedor: externa.Vendedor,
                capturadaEn: externa.CapturadaEn == default ? null : externa.CapturadaEn);
        }
        catch (ExcepcionDominio ex)
        {
            log.LogWarning("Oferta descartada de {Tienda} ({Url}): {Motivo}", externa.Tienda, externa.Url, ex.Message);
            return null;
        }
    }
}
