namespace RepMatch.E2E.Infraestructura;

/// <summary>
/// Configuracion de la corrida, toda por variable de entorno para que el mismo binario corra en
/// la maquina de desarrollo (dotnet run) y dentro de la red de docker compose (scripts/e2e.sh).
/// </summary>
public static class Entorno
{
    /// <summary>La Web principal, como la levanta docker compose: repuestos por SOAP, vehiculos e IA
    /// por REST. Cada busqueda de las pruebas pasa por los dos protocolos.</summary>
    public static string UrlWeb => Leer("E2E_URL_WEB", "http://localhost:5080");

    /// <summary>La Web en modo Local (perfil "local" de compose). Si no responde, las pruebas que
    /// comparan los dos modos se omiten.</summary>
    public static string UrlWebLocal => Leer("E2E_URL_WEB_LOCAL", "http://localhost:5090");

    /// <summary>El servicio SOAP del catalogo, para pedirle el WSDL directamente.</summary>
    public static string UrlCatalogoSoap => Leer("E2E_URL_CATALOGO_SOAP", "http://localhost:5082");

    /// <summary>
    /// Que servicio esta caido a proposito: Normal, SinIA, SinCatalogo, SinSoap o SinTiendas. Lo fija scripts/e2e.sh
    /// en cada fase. Las pruebas de resiliencia se omiten salvo en su escenario, y las comunes se
    /// omiten fuera de Normal, asi una corrida nunca mezcla expectativas.
    /// </summary>
    public static string Escenario => Leer("E2E_ESCENARIO", Escenarios.Normal);

    /// <summary>E2E_VENTANA=1 abre el navegador visible, util para mirar una prueba que falla.</summary>
    public static bool ConVentana => Leer("E2E_VENTANA", "0") == "1";

    /// <summary>Capturas y trazas de cada prueba. Se abren con "playwright show-trace".</summary>
    public static string DirectorioResultados =>
        Leer("E2E_RESULTADOS", Path.Combine(AppContext.BaseDirectory, "resultados"));

    private static string Leer(string variable, string valorPorDefecto)
    {
        var valor = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(valor) ? valorPorDefecto : valor.Trim();
    }
}

public static class Escenarios
{
    public const string Normal = "Normal";
    public const string SinIA = "SinIA";
    public const string SinCatalogo = "SinCatalogo";
    public const string SinSoap = "SinSoap";
    public const string SinTiendas = "SinTiendas";
}

/// <summary>Cuentas de DatosSemilla. Todas con la misma contrasenia.</summary>
public static class Semilla
{
    public const string Contrasena = "repMatch123!";

    public const string EmailCliente = "blofaro@uade.edu.ar";
    public const string NombreCliente = "Bruno Lo Faro";

    public const string EmailMecanicoGratuito = "contacto@tallersanmartin.com.ar";
    public const string EmailMecanicoPro = "carla@motoresbenitez.com.ar";
}
