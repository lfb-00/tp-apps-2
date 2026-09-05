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
    private readonly List<Oferta> _ofertas = [];
    private readonly List<string> _codigosObjetivo = [];

    private Busqueda() { Vehiculo = null!; }   // requerido por EF Core

    public Busqueda(Guid clienteId, DatosVehiculo vehiculo, string textoLibre)
    {
        ExcepcionDominio.Si(clienteId == Guid.Empty, "La busqueda debe pertenecer a un cliente.");
        ArgumentNullException.ThrowIfNull(vehiculo);
        ExcepcionDominio.SiNulaOVacia(textoLibre, nameof(textoLibre));
        ExcepcionDominio.Si(textoLibre.Trim().Length < 5,
            "Describi el problema con un poco mas de detalle (minimo 5 caracteres).");

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

    public void AsignarCodigosObjetivo(IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ExcepcionDominio.Si(Estado is EstadoBusqueda.Completada or EstadoBusqueda.Fallida,
            "No se pueden cambiar los codigos objetivo de una busqueda ya cerrada.");

        var normalizados = codigos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

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

    /// <summary>Ofertas disponibles ordenadas por precio total. Solo compara dentro de una misma
    /// moneda: mezclar ARS con USD sin cotizacion daria un ranking mentiroso.</summary>
    public IReadOnlyList<Oferta> OfertasOrdenadasPorPrecio(string moneda = "ARS") =>
        [.. _ofertas
            .Where(o => o.Disponible && o.PrecioTotal.Moneda == moneda.ToUpperInvariant())
            .OrderBy(o => o.PrecioTotal.Monto)];
}
