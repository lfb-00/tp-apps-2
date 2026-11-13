using RepMatch.Domain.Comun;

namespace RepMatch.Domain.Entidades;

/// <summary>
/// Cuantas derivaciones recibio una tienda en un mes: la base de la liquidacion de la comision de
/// afiliado. Lo arma el consumidor de <c>derivacion.tienda</c> (issue #14), fuera de la peticion
/// de la persona: el clic en "Ir a la tienda" redirige al instante y la cuenta se hace despues.
/// Distingue las derivaciones atribuidas a un mecanico, que es el canal que el modelo de negocio
/// quiere medir.
/// </summary>
public class ComisionesTienda : EntidadBase
{
    private ComisionesTienda() { }   // requerido por EF Core

    public ComisionesTienda(string tienda, string periodo)
    {
        ExcepcionDominio.SiNulaOVacia(tienda, nameof(tienda));
        ExcepcionDominio.Si(!EsPeriodoValido(periodo), "El periodo es un mes con formato AAAA-MM.");

        Tienda = tienda.Trim();
        Periodo = periodo;
    }

    public string Tienda { get; private set; } = string.Empty;

    /// <summary>Mes en formato AAAA-MM, en UTC.</summary>
    public string Periodo { get; private set; } = string.Empty;

    public int Derivaciones { get; private set; }
    public int AtribuidasAMecanico { get; private set; }
    public DateTimeOffset? UltimaEn { get; private set; }

    public static string PeriodoDe(DateTimeOffset momento) => momento.UtcDateTime.ToString("yyyy-MM");

    public void Registrar(bool atribuidaAMecanico, DateTimeOffset ocurridaEn)
    {
        Derivaciones++;
        if (atribuidaAMecanico)
            AtribuidasAMecanico++;
        if (UltimaEn is null || ocurridaEn > UltimaEn)
            UltimaEn = ocurridaEn;
    }

    private static bool EsPeriodoValido(string? periodo) =>
        periodo is { Length: 7 } && periodo[4] == '-'
        && int.TryParse(periodo[..4], out var anio) && anio > 2000
        && int.TryParse(periodo[5..], out var mes) && mes is >= 1 and <= 12;
}

/// <summary>
/// Marca de que un consumidor ya proceso un mensaje. El mismo mensaje puede llegar dos veces (un
/// reintento, una reconexion); se guarda en la misma transaccion que el efecto, asi que o quedan
/// los dos o ninguno, y una segunda entrega se reconoce y no suma de nuevo.
/// </summary>
public class MensajeProcesado
{
    private MensajeProcesado() { }   // requerido por EF Core

    public MensajeProcesado(Guid mensajeId, string consumidor)
    {
        ExcepcionDominio.Si(mensajeId == Guid.Empty, "El mensaje no tiene id.");
        ExcepcionDominio.SiNulaOVacia(consumidor, nameof(consumidor));

        MensajeId = mensajeId;
        Consumidor = consumidor;
        ProcesadoEn = DateTimeOffset.UtcNow;
    }

    public Guid MensajeId { get; private set; }
    public string Consumidor { get; private set; } = string.Empty;
    public DateTimeOffset ProcesadoEn { get; private set; }
}
