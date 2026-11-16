using Microsoft.Extensions.Logging;
using RepMatch.Common.Resultados;
using RepMatch.Contracts;
using RepMatch.Contracts.Configuracion;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Servicios;

/// <summary>
/// Las casas de repuestos cerca de la persona, para cuando una pieza no tiene ofertas en linea:
/// "no tenemos ofertas ahora, pero podes conseguirla en estos lugares". Lee la copia de
/// OpenStreetMap que guarda el worker (<see cref="ServicioImportacionCasas"/>), no el mapa en
/// vivo: la ubicacion del navegador no sale de RepMatch y no se guarda. Solo una zona escrita
/// viaja al mapa, para saber donde queda. Los errores esperables (mapa caido, zona que no existe,
/// casas todavia no importadas) se informan con <see cref="ResultadoOperacion{T}"/>.
/// </summary>
public sealed class ServicioCasasCercanas(
    IMapaCasasRepuestos mapa, ICasaRepuestosRepository casas, OpcionesMapas opciones, ILogger<ServicioCasasCercanas> log)
{
    private const double MetrosPorGradoDeLatitud = 111_320;

    public bool EstaDisponible => mapa.EstaDisponible;

    public async Task<ResultadoOperacion<CasasCercanasDto>> BuscarCercaAsync(UbicacionDto centro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(centro);
        if (!centro.EsValida)
            return ResultadoOperacion<CasasCercanasDto>.Falla("La ubicación no es válida.");

        // Un rectangulo alrededor del punto filtra por indice; despues, la distancia exacta.
        var radio = opciones.RadioMetros;
        var dLat = radio / MetrosPorGradoDeLatitud;
        var dLon = radio / (MetrosPorGradoDeLatitud * Math.Max(Math.Cos(centro.Latitud * Math.PI / 180), 0.01));
        var candidatas = await casas.EnRectanguloAsync(
            centro.Latitud - dLat, centro.Latitud + dLat, centro.Longitud - dLon, centro.Longitud + dLon, ct);

        if (candidatas.Count == 0 && await casas.ContarAsync(ct) == 0)
            return ResultadoOperacion<CasasCercanasDto>.Falla(
                "Todavía estamos cargando las casas de repuestos del mapa. Probá de nuevo en unos minutos.");

        var cercanas = candidatas
            .Select(c => (Casa: c, Metros: Distancia(centro, c)))
            .Where(c => c.Metros <= radio)
            .OrderBy(c => c.Metros)
            .ThenBy(c => c.Casa.Nombre, StringComparer.CurrentCulture)
            .Take(opciones.Maximo)
            .Select(c => new CasaRepuestosDto
            {
                IdMapa = c.Casa.IdMapa,
                Nombre = c.Casa.Nombre,
                Latitud = c.Casa.Latitud,
                Longitud = c.Casa.Longitud,
                DistanciaMetros = (int)Math.Round(c.Metros),
                Direccion = c.Casa.Direccion,
                Telefono = c.Casa.Telefono
            })
            .ToList();

        return ResultadoOperacion<CasasCercanasDto>.Exito(new CasasCercanasDto
        {
            Centro = centro,
            Casas = cercanas,
            RadioMetros = radio
        });
    }

    /// <summary>Ubica una zona escrita ("Palermo", "Rosario") y busca alrededor de su centro.</summary>
    public async Task<ResultadoOperacion<CasasCercanasDto>> BuscarCercaDeAsync(string zona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zona) || zona.Trim().Length < 3)
            return ResultadoOperacion<CasasCercanasDto>.Falla("Escribí un barrio o una ciudad.");

        UbicacionDto? centro;
        try
        {
            centro = await mapa.UbicarAsync(zona.Trim(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("No se pudo ubicar una zona en el mapa: {Motivo}", ex.Message);
            return ResultadoOperacion<CasasCercanasDto>.Falla(
                "No pudimos ubicar la zona en el mapa. Probá de nuevo en unos minutos o usá tu ubicación.");
        }

        if (centro is null)
            return ResultadoOperacion<CasasCercanasDto>.Falla($"No encontramos \"{zona.Trim()}\". Probá con el barrio y la ciudad.");

        return await BuscarCercaAsync(centro, ct);
    }

    private static double Distancia(UbicacionDto centro, CasaRepuestos casa)
    {
        static double Radianes(double grados) => grados * Math.PI / 180;
        var dLat = Radianes(casa.Latitud - centro.Latitud);
        var dLon = Radianes(casa.Longitud - centro.Longitud);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Radianes(centro.Latitud)) * Math.Cos(Radianes(casa.Latitud)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * 6_371_000 * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>
/// Trae las casas de repuestos de Argentina del mapa y reemplaza la copia guardada. Lo corre el
/// worker al arrancar si la copia falta o es vieja, y despues cada <see cref="OpcionesMapas.RefrescoDias"/>.
/// Si el mapa no responde o responde vacio, la copia anterior queda como esta.
/// </summary>
public sealed class ServicioImportacionCasas(
    IMapaCasasRepuestos mapa,
    ICasaRepuestosRepository casas,
    IUnitOfWork unidadDeTrabajo,
    OpcionesMapas opciones,
    ILogger<ServicioImportacionCasas> log,
    TimeProvider? reloj = null)
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    /// <summary>true si no hay copia o tiene mas de <see cref="OpcionesMapas.RefrescoDias"/> dias.</summary>
    public async Task<bool> HaceFaltaImportarAsync(CancellationToken ct = default)
    {
        var ultima = await casas.UltimaImportacionAsync(ct);
        return ultima is null || _reloj.GetUtcNow() - ultima.Value >= TimeSpan.FromDays(opciones.RefrescoDias);
    }

    public async Task<ResultadoOperacion<int>> ImportarAsync(CancellationToken ct = default)
    {
        if (!mapa.EstaDisponible)
            return ResultadoOperacion<int>.Falla("No hay mapa configurado.");

        IReadOnlyList<CasaRepuestosDto> descargadas;
        string? falla = null;
        try
        {
            descargadas = await mapa.DescargarCasasAsync(ct);
            // Una respuesta vacia de todo el pais es una falla del servidor, no que cerraron todas.
            if (descargadas.Count == 0)
                falla = "El mapa respondió sin casas de repuestos.";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            descargadas = [];
            falla = ex.Message;
        }

        if (falla is not null)
        {
            // Con la base vacia (primer arranque) se carga la copia incluida, para no esperar a que
            // Overpass vuelva. Igual se informa la falla: el worker reintenta y la reemplaza.
            if (await casas.ContarAsync(ct) == 0 && mapa.CopiaIncluida() is { Casas.Count: > 0 } copia)
            {
                var cargadas = await GuardarAsync(copia.Casas, copia.Fecha, ct);
                log.LogWarning(
                    "No se pudieron descargar las casas de repuestos ({Motivo}). Se cargo la copia incluida del {Fecha:yyyy-MM-dd}: {Cantidad} casas.",
                    falla, copia.Fecha, cargadas);
            }
            else
            {
                log.LogWarning("No se pudieron descargar las casas de repuestos ({Motivo}). Queda la copia anterior.", falla);
            }

            return ResultadoOperacion<int>.Falla(falla);
        }

        var importadas = await GuardarAsync(descargadas, _reloj.GetUtcNow(), ct);
        log.LogInformation("Casas de repuestos importadas de OpenStreetMap: {Cantidad}", importadas);
        return ResultadoOperacion<int>.Exito(importadas);
    }

    private async Task<int> GuardarAsync(IReadOnlyList<CasaRepuestosDto> descargadas, DateTimeOffset fecha, CancellationToken ct)
    {
        var ahora = fecha;
        var nuevas = new List<CasaRepuestos>(descargadas.Count);
        foreach (var casa in descargadas.DistinctBy(c => c.IdMapa))
        {
            try
            {
                nuevas.Add(new CasaRepuestos(casa.IdMapa, casa.Nombre, casa.Latitud, casa.Longitud,
                    Recortar(casa.Direccion, 300), Recortar(casa.Telefono, 80), ahora));
            }
            catch (ExcepcionDominio ex)
            {
                log.LogDebug("Casa {IdMapa} descartada: {Motivo}", casa.IdMapa, ex.Message);
            }
        }

        await casas.ReemplazarTodasAsync(nuevas, ct);
        await unidadDeTrabajo.ConfirmarAsync(ct);
        return nuevas.Count;
    }

    private static string? Recortar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}

/// <summary>Sin mapa configurado (o en un host que no lo usa): la pantalla no muestra la seccion.</summary>
public sealed class MapaDesactivado : IMapaCasasRepuestos
{
    public bool EstaDisponible => false;

    public Task<IReadOnlyList<CasaRepuestosDto>> DescargarCasasAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CasaRepuestosDto>>([]);

    public (IReadOnlyList<CasaRepuestosDto> Casas, DateTimeOffset Fecha)? CopiaIncluida() => null;

    public Task<UbicacionDto?> UbicarAsync(string zona, CancellationToken ct = default) =>
        Task.FromResult<UbicacionDto?>(null);
}
