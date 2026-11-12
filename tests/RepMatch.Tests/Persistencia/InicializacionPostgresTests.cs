using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RepMatch.Persistence;

namespace RepMatch.Tests.Persistencia;

public sealed class InicializacionPostgresTests
{
    [PostgresFact]
    public async Task Dos_hosts_inicializan_en_paralelo_la_misma_base_sin_duplicar_la_semilla()
    {
        var cadenaAdministrativa = Environment.GetEnvironmentVariable(
            PostgresFactAttribute.VariableConexion)
            ?? throw new InvalidOperationException(
                $"La prueba requiere la variable {PostgresFactAttribute.VariableConexion}.");
        var nombreBase = $"repmatch_test_{Guid.NewGuid():N}";
        var cadenaBase = new NpgsqlConnectionStringBuilder(cadenaAdministrativa)
        {
            Database = nombreBase
        }.ConnectionString;

        await CrearBaseAsync(cadenaAdministrativa, nombreBase);

        try
        {
            await using var hostUno = CrearHost(cadenaBase);
            await using var hostDos = CrearHost(cadenaBase);

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
        finally
        {
            await EliminarBaseAsync(cadenaAdministrativa, nombreBase);
        }
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

    private static async Task CrearBaseAsync(string cadenaAdministrativa, string nombreBase)
    {
        await using var conexion = new NpgsqlConnection(cadenaAdministrativa);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"CREATE DATABASE \"{nombreBase}\"";
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task EliminarBaseAsync(string cadenaAdministrativa, string nombreBase)
    {
        await using var conexion = new NpgsqlConnection(cadenaAdministrativa);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"DROP DATABASE IF EXISTS \"{nombreBase}\" WITH (FORCE)";
        await comando.ExecuteNonQueryAsync();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string VariableConexion = "REPMATCH_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariableConexion)))
            Skip = $"Configura {VariableConexion} para ejecutar la prueba de integración con PostgreSQL.";
    }
}
