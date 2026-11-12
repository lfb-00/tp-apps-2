using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RepMatch.Domain.Repositorios;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Persistence;

/// <summary>Proveedor de almacenamiento, elegido por configuracion.</summary>
public enum ProveedorPersistencia
{
    /// <summary>En memoria. Permite correr y demostrar la app sin Docker ni Postgres.</summary>
    InMemory = 0,

    /// <summary>PostgreSQL, el proveedor del entorno containerizado.</summary>
    Postgres = 1
}

/// <summary>
/// Punto unico de registro del componente de acceso a datos. Los hosts (Web y Catalogo.Api) lo
/// llaman con su IConfiguration y no necesitan saber nada de EF Core ni del proveedor concreto.
/// </summary>
public static class ExtensionesPersistencia
{
    private const long ClaveBloqueoInicializacion = 0x524D5041544348;
    private static readonly string[] TablasEsquemaInicial =
    [
        "aplicaciones_vehiculo",
        "busquedas",
        "clientes",
        "configuraciones_vehiculo",
        "derivaciones",
        "ofertas",
        "repuestos",
        "vehiculos"
    ];

    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var proveedor = configuracion.GetValue("Persistencia:Proveedor", ProveedorPersistencia.InMemory);
        var cadena = configuracion.GetConnectionString("RepMatch");

        if (proveedor == ProveedorPersistencia.Postgres && string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException(
                "Persistencia:Proveedor=Postgres requiere la cadena de conexion 'ConnectionStrings:RepMatch'.");

        servicios.AddDbContext<RepMatchDbContext>(opciones =>
        {
            if (proveedor == ProveedorPersistencia.Postgres)
                opciones.UseNpgsql(cadena);
            else
                opciones.UseInMemoryDatabase("repmatch");
        });

        servicios.AddScoped<IClienteRepository, ClienteRepository>();
        servicios.AddScoped<IRepuestoRepository, RepuestoRepository>();
        servicios.AddScoped<IConfiguracionVehiculoRepository, ConfiguracionVehiculoRepository>();
        servicios.AddScoped<IBusquedaRepository, BusquedaRepository>();
        servicios.AddScoped<IDerivacionRepository, DerivacionRepository>();
        servicios.AddScoped<IComisionesRepository, ComisionesRepository>();
        servicios.AddScoped<IUnitOfWork, UnitOfWork>();

        return servicios;
    }

    /// <summary>
    /// Actualiza el esquema y siembra el catalogo. PostgreSQL serializa este trabajo entre hosts;
    /// las bases creadas antes de las migraciones se adoptan solo si tienen el esquema inicial completo.
    /// </summary>
    public static async Task InicializarBaseAsync(
        this IServiceProvider servicios, CancellationToken ct = default)
    {
        using var alcance = servicios.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<RepMatchDbContext>();
        var log = alcance.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ExtensionesPersistencia));

        const int intentosMaximos = 10;

        for (var intento = 1; intento <= intentosMaximos; intento++)
        {
            try
            {
                if (contexto.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
                    await InicializarPostgresAsync(contexto, log, ct);
                else
                {
                    await contexto.Database.EnsureCreatedAsync(ct);
                    await DatosSemilla.SembrarAsync(contexto, ct);
                }

                log.LogInformation("Base de datos lista (proveedor {Proveedor}).",
                    contexto.Database.ProviderName);
                return;
            }
            catch (NpgsqlException ex) when (intento < intentosMaximos && ex.IsTransient)
            {
                var espera = TimeSpan.FromSeconds(2 * intento);
                log.LogWarning(ex, "La base no esta disponible (intento {Intento}/{Max}). Reintento en {Espera}s.",
                    intento, intentosMaximos, espera.TotalSeconds);
                await Task.Delay(espera, ct);
            }
        }

        throw new InvalidOperationException(
            $"No se pudo inicializar la base de datos tras {intentosMaximos} intentos.");
    }

    private static async Task InicializarPostgresAsync(
        RepMatchDbContext contexto, ILogger log, CancellationToken ct)
    {
        var baseDatos = contexto.Database;
        await baseDatos.OpenConnectionAsync(ct);
        var bloqueoAdquirido = false;

        try
        {
            await EjecutarBloqueoAdvisoryAsync(contexto, adquirir: true, ct);
            bloqueoAdquirido = true;

            await AdoptarEsquemaExistenteAsync(contexto, log, ct);
            await baseDatos.MigrateAsync(ct);
            await DatosSemilla.SembrarAsync(contexto, ct);
        }
        finally
        {
            try
            {
                if (bloqueoAdquirido)
                    await EjecutarBloqueoAdvisoryAsync(contexto, adquirir: false, CancellationToken.None);
            }
            finally
            {
                await baseDatos.CloseConnectionAsync();
            }
        }
    }

    private static async Task AdoptarEsquemaExistenteAsync(
        RepMatchDbContext contexto, ILogger log, CancellationToken ct)
    {
        var conexion = contexto.Database.GetDbConnection();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            """;

        var tablas = new HashSet<string>(StringComparer.Ordinal);
        await using (var lector = await comando.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
                tablas.Add(lector.GetString(0));
        }

        var tieneHistorial = tablas.Contains("__EFMigrationsHistory");
        var tieneEsquemaAnterior = TablasEsquemaInicial.Any(tablas.Contains);
        if (!tieneEsquemaAnterior)
            return;

        var faltantes = TablasEsquemaInicial.Where(tabla => !tablas.Contains(tabla)).ToArray();
        if (faltantes.Length > 0)
        {
            throw new InvalidOperationException(
                "La base PostgreSQL tiene un esquema parcial y no se puede adoptar como esquema inicial. " +
                $"Faltan tablas: {string.Join(", ", faltantes)}.");
        }

        if (tieneHistorial)
        {
            comando.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory" """;
            if (Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) != 0)
                return;
        }

        var migracionInicial = contexto.Database.GetMigrations().FirstOrDefault()
            ?? throw new InvalidOperationException("No hay una migracion inicial para adoptar el esquema existente.");

        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            )
            """;
        await comando.ExecuteNonQueryAsync(ct);

        comando.CommandText = """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES (@migration, @version)
            ON CONFLICT ("MigrationId") DO NOTHING
            """;
        var parametroMigracion = comando.CreateParameter();
        parametroMigracion.ParameterName = "migration";
        parametroMigracion.DbType = DbType.String;
        parametroMigracion.Value = migracionInicial;
        comando.Parameters.Add(parametroMigracion);

        var parametroVersion = comando.CreateParameter();
        parametroVersion.ParameterName = "version";
        parametroVersion.DbType = DbType.String;
        parametroVersion.Value = contexto.Model.GetProductVersion();
        comando.Parameters.Add(parametroVersion);
        await comando.ExecuteNonQueryAsync(ct);

        log.LogWarning(
            "Se adopto el esquema PostgreSQL existente como migracion inicial; las migraciones pendientes actualizaran las tablas nuevas.");
    }

    private static async Task EjecutarBloqueoAdvisoryAsync(
        RepMatchDbContext contexto, bool adquirir, CancellationToken ct)
    {
        await using var comando = contexto.Database.GetDbConnection().CreateCommand();
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "clave";
        parametro.Value = ClaveBloqueoInicializacion;
        comando.Parameters.Add(parametro);
        comando.CommandText = adquirir
            ? "SELECT pg_advisory_lock(@clave)"
            : "SELECT pg_advisory_unlock(@clave)";

        var resultado = await comando.ExecuteScalarAsync(ct);
        if (!adquirir && resultado is not true)
            throw new InvalidOperationException("No se pudo liberar el bloqueo de inicializacion de PostgreSQL.");
    }
}
