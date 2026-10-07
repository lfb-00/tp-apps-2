using Microsoft.AspNetCore.DataProtection;

namespace RepMatch.Web.Servicios;

/// <summary>
/// Claves con las que ASP.NET Core cifra la sesion guardada en el navegador
/// (<see cref="SesionActual"/>) y los tokens antiforgery.
///
/// Dentro de un contenedor, por defecto, quedan en el disco del contenedor: cada
/// <c>docker compose up --build</c> las regenera, las pestanias abiertas pierden la sesion y el log
/// se llena de errores de antiforgery. Con <c>ProteccionDatos:DirectorioClaves</c> (variable
/// <c>ProteccionDatos__DirectorioClaves</c>) se guardan en ese directorio, que en compose es un
/// volumen. Fuera de Docker no hace falta configurarlo: ASP.NET Core ya las guarda en el perfil
/// del usuario.
///
/// Las claves quedan sin cifrar en el volumen (ASP.NET lo advierte en el log al arrancar). Para el
/// laboratorio alcanza; en un despliegue real se protegerian con un certificado o un key vault.
/// </summary>
public static class ExtensionesProteccionDatos
{
    public static IServiceCollection AgregarProteccionDatos(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var directorio = configuracion["ProteccionDatos:DirectorioClaves"];
        if (!string.IsNullOrWhiteSpace(directorio))
        {
            servicios.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(directorio));
        }

        return servicios;
    }
}
