using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RepMatch.Persistence;
using RepMatch.Tests.Integracion;
using Testcontainers.PostgreSql;

namespace RepMatch.Tests.Persistencia;

/// <summary>Un PostgreSQL real para todas las pruebas de adopcion; cada prueba usa su propia base.</summary>
public sealed class PostgresAdopcionFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("repmatch")
        .WithUsername("repmatch")
        .WithPassword("repmatch")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public async Task<string> NuevaBaseAsync()
    {
        var nombre = $"adopcion_{Guid.NewGuid():N}"[..20];
        await using (var conexion = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await conexion.OpenAsync();
            await using var comando = new NpgsqlCommand($"CREATE DATABASE {nombre}", conexion);
            await comando.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = nombre }.ToString();
    }
}

/// <summary>
/// Adopcion de una base creada con EnsureCreatedAsync, antes de que hubiera migraciones. Se adopta
/// solo si tiene el esquema inicial completo: una base anterior a las ofertas tiene las mismas
/// tablas pero le faltan columnas, y adoptarla dejaria la app fallando en la primera consulta.
/// </summary>
public sealed class AdopcionEsquemaTests(PostgresAdopcionFixture postgres) : IClassFixture<PostgresAdopcionFixture>
{
    [RequiereDockerFact]
    public async Task Una_base_con_el_esquema_inicial_completo_y_sin_historial_se_adopta_y_se_migra()
    {
        var cadena = await postgres.NuevaBaseAsync();
        await CrearEsquemaInicialSinHistorialAsync(cadena);

        await using var servicios = Host(cadena);
        await servicios.InicializarBaseAsync();

        Assert.Equal(2, await EscalarAsync(cadena, """SELECT COUNT(*) FROM "__EFMigrationsHistory" """));
        Assert.Equal(1, await EscalarAsync(cadena,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'comisiones_tienda'"));
        Assert.True(await EscalarAsync(cadena, "SELECT COUNT(*) FROM clientes") > 0);
    }

    [RequiereDockerFact]
    public async Task Una_base_anterior_a_las_ofertas_no_se_adopta_y_pide_recrearla()
    {
        var cadena = await postgres.NuevaBaseAsync();
        await CrearEsquemaInicialSinHistorialAsync(cadena);
        // Las columnas que agrego la entrega de ofertas: una base de la epoca de SOAP no las tiene.
        await EjecutarAsync(cadena, """
            ALTER TABLE ofertas DROP COLUMN "Ean";
            ALTER TABLE busquedas DROP COLUMN "PreciosConsultadosEn";
            """);

        await using var servicios = Host(cadena);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => servicios.InicializarBaseAsync());

        Assert.Contains("ofertas.Ean", error.Message);
        Assert.Contains("busquedas.PreciosConsultadosEn", error.Message);
        Assert.Contains("down -v", error.Message);
        Assert.Equal(0, await EscalarAsync(cadena,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory'"));
    }

    /// <summary>Lo que dejaba EnsureCreatedAsync: el esquema de la migracion inicial, sin historial.</summary>
    private static async Task CrearEsquemaInicialSinHistorialAsync(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<RepMatchDbContext>().UseNpgsql(cadena).Options;
        await using (var contexto = new RepMatchDbContext(opciones))
        {
            var inicial = contexto.Database.GetMigrations().First();
            await contexto.GetService<IMigrator>().MigrateAsync(inicial);
        }

        await EjecutarAsync(cadena, """DROP TABLE "__EFMigrationsHistory" """);
    }

    private static ServiceProvider Host(string cadena)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistencia:Proveedor"] = "Postgres",
                ["ConnectionStrings:RepMatch"] = cadena
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AgregarPersistencia(configuracion);
        return servicios.BuildServiceProvider();
    }

    private static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<long> EscalarAsync(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }
}
