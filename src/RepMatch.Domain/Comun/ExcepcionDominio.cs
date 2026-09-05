namespace RepMatch.Domain.Comun;

/// <summary>
/// Se lanza cuando se intenta violar una invariante del dominio. La capa de presentacion la
/// traduce a un error de usuario; nunca deberia escapar como error 500.
/// </summary>
public sealed class ExcepcionDominio(string mensaje) : Exception(mensaje)
{
    public static void SiNulaOVacia(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ExcepcionDominio($"El campo '{campo}' es obligatorio.");
    }

    public static void Si(bool condicion, string mensaje)
    {
        if (condicion) throw new ExcepcionDominio(mensaje);
    }
}
