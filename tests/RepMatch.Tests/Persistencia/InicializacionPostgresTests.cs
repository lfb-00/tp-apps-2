using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Persistence;
using RepMatch.Tests.Integracion;

namespace RepMatch.Tests.Persistencia;

/// <summary>
/// En compose varios hosts (Catalogo.Api, Catalogo.Soap, Web, Worker) arrancan contra la misma base.
/// El bloqueo advisory de InicializarBaseAsync tiene que serializar migraciones y semilla: sin el,
/// dos hosts sembrarian a la vez y se duplicarian los clientes o fallaria una restriccion unica.
/// Reusa el PostgreSQL de las pruebas de adopcion, con una base propia.
/// </summary>
public sealed class InicializacionPostgresTests(PostgresAdopcionFixture postgres)
    : IClassFixture<PostgresAdopcionFixture>
{
    [RequiereDockerFact]
    public async Task Dos_hosts_inicializan_en_paralelo_la_misma_base_sin_duplicar_la_semilla()
    {
        var cadena = await postgres.NuevaBaseAsync();

        await using var hostUno = CrearHost(cadena);
        await using var hostDos = CrearHost(cadena);

        await Task.WhenAll(
            hostUno.InicializarBaseAsync(),
            hostDos.InicializarBaseAsync());

        await using var alcance = hostUno.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<RepMatchDbContext>();

        Assert.Equal(3, await contexto.Clientes.CountAsync());
        Assert.Equal(1, await contexto.Clientes.CountAsync(c => c.Email == "blofaro@uade.edu.ar"));
        Assert.NotEmpty(await contexto.Repuestos.ToListAsync());
        Assert.NotEmpty(await contexto.ConfiguracionesVehiculo.ToListAsync());
    }

    private static ServiceProvider CrearHost(string cadenaConexion)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistencia:Proveedor"] = nameof(ProveedorPersistencia.Postgres),
                ["ConnectionStrings:RepMatch"] = cadenaConexion
            })
            .Build();
        servicios.AgregarPersistencia(configuracion);

        return servicios.BuildServiceProvider();
    }
}
