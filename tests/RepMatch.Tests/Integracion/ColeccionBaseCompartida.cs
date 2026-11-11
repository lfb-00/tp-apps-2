namespace RepMatch.Tests.Integracion;

/// <summary>
/// Las pruebas que hospedan Catalogo.Api y Catalogo.Soap van en una sola coleccion para que xUnit no
/// las corra en paralelo. Los dos hosts usan la misma base InMemory ("repmatch", el nombre lo fija
/// AgregarPersistencia) y la siembran al arrancar con "si esta vacia, cargar": si arrancan a la vez,
/// los dos la ven vacia y el catalogo queda duplicado, porque InMemory no tiene indices unicos ni
/// transacciones que lo frenen. En Postgres no pasa: la siembra va en una transaccion que choca con
/// los indices unicos. Pasaba de vez en cuando desde que se sumo el host SOAP.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionBaseCompartida
{
    public const string Nombre = "Base InMemory compartida por los hosts";
}
