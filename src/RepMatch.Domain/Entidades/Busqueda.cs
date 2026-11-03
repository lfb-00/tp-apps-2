using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// La solicitud del cliente: su vehiculo mas el problema descripto en texto libre. Es el agregado
/// que atraviesa todo el sistema y equivale al componente "Pedido" que pide la consigna, aunque
/// aca no se compra nada: lo que se "despacha" es la consulta a las tiendas.
/// </summary>
public class Busqueda : EntidadBase
{
    public const int LongitudMaximaTexto = 500;
    public const int LongitudMaximaNota = 1000;
    private readonly List<Oferta> _ofertas = [];
    private readonly List<string> _codigosObjetivo = [];
    private readonly List<string> _codigosRecomendados = [];

    private Busqueda() { Vehiculo = null!; }   // requerido por EF Core

    public Busqueda(Guid clienteId, DatosVehiculo vehiculo, string textoLibre)
    {
        ExcepcionDominio.Si(clienteId == Guid.Empty, "La busqueda debe pertenecer a un cliente.");
        ArgumentNullException.ThrowIfNull(vehiculo);
        ExcepcionDominio.SiNulaOVacia(textoLibre, nameof(textoLibre));
        ExcepcionDominio.Si(textoLibre.Trim().Length < 5,
            "Describi el problema con un poco mas de detalle (minimo 5 caracteres).");
        ExcepcionDominio.Si(textoLibre.Trim().Length > LongitudMaximaTexto,
            "La descripción admite hasta 500 caracteres.");

        ClienteId = clienteId;
        Vehiculo = vehiculo;
        TextoLibre = textoLibre.Trim();
        Estado = EstadoBusqueda.Pendiente;
        FechaCreacion = DateTimeOffset.UtcNow;

        RegistrarEvento(new BusquedaCreada(Id, ClienteId, Vehiculo, TextoLibre));
    }

    public Guid ClienteId { get; private set; }
    public DatosVehiculo Vehiculo { get; private set; }
    public string TextoLibre { get; private set; } = string.Empty;
    public EstadoBusqueda Estado { get; private set; }
    public DateTimeOffset FechaCreacion { get; private set; }
    public string? MotivoFalla { get; private set; }

    /// <summary>Codigos de repuesto a buscar. En el TP Inicial los carga el catalogo por
    /// compatibilidad; en la Segunda Parte los propone el componente de IA.</summary>
    public IReadOnlyCollection<string> CodigosObjetivo => _codigosObjetivo.AsReadOnly();

    public IReadOnlyCollection<Oferta> Ofertas => _ofertas.AsReadOnly();

    /// <summary>Mecanico al que el cliente le compartio la busqueda. Solo el lo ve, ademas del
    /// duenio: compartir es una decision del cliente, no algo que el mecanico pueda pedir.</summary>
    public Guid? MecanicoId { get; private set; }
    public DateTimeOffset? CompartidaEn { get; private set; }

    /// <summary>true si el mecanico confirmo el diagnostico, false si lo corrigio, null si
    /// todavia no la reviso.</summary>
    public bool? DiagnosticoConfirmado { get; private set; }

    /// <summary>Lo que el mecanico recomienda comprar. Se guarda aparte de
    /// <see cref="CodigosObjetivo"/> a proposito: el par "propuesto por el sistema / validado por
    /// un profesional" es el dato etiquetado con que se mide la exactitud de la IA.</summary>
    public IReadOnlyCollection<string> CodigosRecomendados => _codigosRecomendados.AsReadOnly();

    public string? NotaMecanico { get; private set; }
    public DateTimeOffset? RevisadaEn { get; private set; }
    public bool EstaRevisada => RevisadaEn is not null;
    public bool EstaCompartida => MecanicoId is not null;

    public void CompartirCon(Guid mecanicoId)
    {
        ExcepcionDominio.Si(mecanicoId == Guid.Empty, "Indicá el mecánico con quien compartir.");
        ExcepcionDominio.Si(mecanicoId == ClienteId, "No podés compartirte una consulta a vos mismo.");
        ExcepcionDominio.Si(EstaRevisada, "La consulta ya fue revisada por un mecánico.");
        ExcepcionDominio.Si(MecanicoId == mecanicoId, "La consulta ya está compartida con ese mecánico.");

        MecanicoId = mecanicoId;
        CompartidaEn = DateTimeOffset.UtcNow;

        RegistrarEvento(new BusquedaCompartida(Id, ClienteId, mecanicoId));
    }

    /// <summary>El cliente la retira de la bandeja del mecanico. Una vez revisada ya no se puede:
    /// la revision queda como historial de los dos.</summary>
    public void DejarDeCompartir()
    {
        ExcepcionDominio.Si(!EstaCompartida, "La consulta no está compartida con ningún mecánico.");
        ExcepcionDominio.Si(EstaRevisada, "La consulta ya fue revisada: queda en el historial.");

        MecanicoId = null;
        CompartidaEn = null;
    }

    /// <summary>
    /// El mecanico confirma lo que propuso el sistema o lo corrige con sus propios codigos. Si
    /// confirma, recomienda exactamente los codigos objetivo; si corrige, tiene que decir cuales.
    /// </summary>
    public void RegistrarRevision(
        Guid mecanicoId, bool confirmaDiagnostico, IEnumerable<string>? codigosRecomendados, string? nota)
    {
        ExcepcionDominio.Si(!EstaCompartida || MecanicoId != mecanicoId,
            "La consulta no está compartida con este mecánico.");
        ExcepcionDominio.Si(EstaRevisada, "La consulta ya fue revisada.");

        var notaLimpia = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        ExcepcionDominio.Si(notaLimpia?.Length > LongitudMaximaNota, "La nota admite hasta 1000 caracteres.");

        List<string> recomendados;
        if (confirmaDiagnostico)
        {
            ExcepcionDominio.Si(_codigosObjetivo.Count == 0,
                "No hay diagnóstico para confirmar: indicá los repuestos que recomendás.");
            recomendados = [.. _codigosObjetivo];
        }
        else
        {
            recomendados = Normalizar(codigosRecomendados ?? []);
            ExcepcionDominio.Si(recomendados.Count == 0,
                "Para corregir el diagnóstico indicá al menos un repuesto.");
        }

        DiagnosticoConfirmado = confirmaDiagnostico;
        _codigosRecomendados.Clear();
        _codigosRecomendados.AddRange(recomendados);
        NotaMecanico = notaLimpia;
        RevisadaEn = DateTimeOffset.UtcNow;

        RegistrarEvento(new DiagnosticoValidado(
            Id, ClienteId, mecanicoId, confirmaDiagnostico, [.. _codigosObjetivo], recomendados));
    }

    /// <summary>
    /// Registra que el usuario salio hacia la tienda de una oferta. Es el hecho que genera la
    /// comision de afiliado. Si la oferta es de un repuesto que recomendo el mecanico que reviso la
    /// busqueda, la derivacion se le atribuye. No altera el orden de las ofertas: el ranking
    /// depende solo del precio, nunca de quien cobra.
    /// </summary>
    public Derivacion Derivar(Guid ofertaId)
    {
        var oferta = _ofertas.SingleOrDefault(o => o.Id == ofertaId)
            ?? throw new ExcepcionDominio("La oferta no pertenece a esta búsqueda.");

        var mecanico = EstaRevisada && _codigosRecomendados.Contains(oferta.CodigoRepuesto)
            ? MecanicoId
            : null;

        return new Derivacion(Id, oferta.Id, oferta.NombreTienda, oferta.UrlOriginal, mecanico);
    }

    public void AsignarCodigosObjetivo(IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ExcepcionDominio.Si(Estado is EstadoBusqueda.Completada or EstadoBusqueda.Fallida,
            "No se pueden cambiar los codigos objetivo de una busqueda ya cerrada.");

        var normalizados = Normalizar(codigos);

        ExcepcionDominio.Si(normalizados.Count == 0, "Hay que indicar al menos un codigo objetivo.");

        _codigosObjetivo.Clear();
        _codigosObjetivo.AddRange(normalizados);
        Estado = EstadoBusqueda.Diagnosticada;
    }

    public void RegistrarOfertas(IEnumerable<Oferta> ofertas)
    {
        ArgumentNullException.ThrowIfNull(ofertas);
        ExcepcionDominio.Si(Estado == EstadoBusqueda.Fallida,
            "No se pueden agregar ofertas a una busqueda fallida.");

        _ofertas.AddRange(ofertas);
        Estado = EstadoBusqueda.Completada;
    }

    public void Fallar(string motivo)
    {
        ExcepcionDominio.SiNulaOVacia(motivo, nameof(motivo));
        Estado = EstadoBusqueda.Fallida;
        MotivoFalla = motivo.Trim();
    }

    private static List<string> Normalizar(IEnumerable<string> codigos) =>
        [.. codigos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()];

    /// <summary>Ofertas disponibles ordenadas por precio total. Solo compara dentro de una misma
    /// moneda: mezclar ARS con USD sin cotizacion daria un ranking mentiroso.</summary>
    public IReadOnlyList<Oferta> OfertasOrdenadasPorPrecio(string moneda = "ARS") =>
        [.. _ofertas
            .Where(o => o.Disponible && o.PrecioTotal.Moneda == moneda.ToUpperInvariant())
            .OrderBy(o => o.PrecioTotal.Monto)];
}
