using RepMatch.Domain.Comun;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Repuesto canonico del catalogo: la pieza como concepto, independiente de quien la venda.
/// Equivale al componente "Producto" de la consigna. Su contracara es <see cref="Oferta"/>,
/// que es esa misma pieza publicada por una tienda concreta a un precio concreto.
/// </summary>
public class Repuesto : EntidadBase
{
    private readonly List<AplicacionVehiculo> _aplicaciones = [];
    private readonly List<string> _codigosEquivalentes = [];

    private Repuesto() { }   // requerido por EF Core

    public Repuesto(string codigoCanonico, string nombre, SistemaVehiculo sistema, string? descripcion = null)
    {
        ExcepcionDominio.SiNulaOVacia(codigoCanonico, nameof(codigoCanonico));
        ExcepcionDominio.SiNulaOVacia(nombre, nameof(nombre));
        ExcepcionDominio.Si(sistema == SistemaVehiculo.Desconocido,
            "Un repuesto del catalogo debe pertenecer a un sistema conocido del vehiculo.");

        CodigoCanonico = codigoCanonico.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Sistema = sistema;
        Descripcion = descripcion?.Trim();
    }

    public string CodigoCanonico { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public SistemaVehiculo Sistema { get; private set; }

    public IReadOnlyCollection<AplicacionVehiculo> Aplicaciones => _aplicaciones.AsReadOnly();

    /// <summary>Codigos OEM y de fabricantes alternativos que designan la misma pieza. Se usan
    /// para ampliar la consulta a las tiendas y para deduplicar ofertas.</summary>
    public IReadOnlyCollection<string> CodigosEquivalentes => _codigosEquivalentes.AsReadOnly();

    public void AgregarAplicacion(AplicacionVehiculo aplicacion)
    {
        ArgumentNullException.ThrowIfNull(aplicacion);
        _aplicaciones.Add(aplicacion);
    }

    public void AgregarEquivalencia(string codigo)
    {
        ExcepcionDominio.SiNulaOVacia(codigo, nameof(codigo));

        var normalizado = codigo.Trim().ToUpperInvariant();
        ExcepcionDominio.Si(normalizado == CodigoCanonico,
            "El codigo canonico no puede figurar tambien como equivalencia.");

        if (!_codigosEquivalentes.Contains(normalizado))
            _codigosEquivalentes.Add(normalizado);
    }

    public bool EsCompatibleCon(DatosVehiculo vehiculo) =>
        _aplicaciones.Any(a => a.EsCompatibleCon(vehiculo));

    /// <summary>Terminos con los que conviene consultar a las tiendas externas.</summary>
    public IReadOnlyList<string> TerminosDeBusqueda() =>
        [CodigoCanonico, .. _codigosEquivalentes, Nombre];
}
