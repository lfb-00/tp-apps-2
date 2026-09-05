using RepMatch.Domain.Comun;

namespace RepMatch.Domain.ValueObjects;

/// <summary>
/// Importe con moneda. Existe porque el comparador mezcla ofertas en ARS (tiendas VTEX) y en
/// USD (eBay): sumar o comparar importes de distinta moneda debe ser un error explicito, no un
/// bug silencioso.
/// </summary>
public sealed record Dinero
{
    public decimal Monto { get; }
    public string Moneda { get; }

    public Dinero(decimal monto, string moneda)
    {
        ExcepcionDominio.Si(monto < 0, "El monto no puede ser negativo.");
        ExcepcionDominio.SiNulaOVacia(moneda, nameof(moneda));
        ExcepcionDominio.Si(moneda.Trim().Length != 3, "La moneda debe ser un codigo ISO-4217 de 3 letras.");

        Monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero);
        Moneda = moneda.Trim().ToUpperInvariant();
    }

    public static Dinero Pesos(decimal monto) => new(monto, "ARS");
    public static Dinero Dolares(decimal monto) => new(monto, "USD");

    public static Dinero operator +(Dinero a, Dinero b)
    {
        ExcepcionDominio.Si(a.Moneda != b.Moneda,
            $"No se pueden sumar importes de distinta moneda ({a.Moneda} y {b.Moneda}).");
        return new Dinero(a.Monto + b.Monto, a.Moneda);
    }

    public bool EsMasBaratoQue(Dinero otro)
    {
        ExcepcionDominio.Si(Moneda != otro.Moneda,
            $"No se pueden comparar importes de distinta moneda ({Moneda} y {otro.Moneda}).");
        return Monto < otro.Monto;
    }

    public override string ToString() => $"{Moneda} {Monto:N2}";
}
