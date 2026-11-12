using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    // Todos los hosts usan la misma clave para serializar la inicializacion de la base compartida.
    private const long ClaveBloqueoInicializacion = 0x524D5041544348;

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
        servicios.AddScoped<IUnitOfWork, UnitOfWork>();

        return servicios;
    }

    /// <summary>
    /// Crea el esquema si hace falta y siembra el catalogo. Con Postgres reintenta si la base
    /// todavia no acepta conexiones y serializa la inicializacion entre los hosts concurrentes.
    /// </summary>
    public static async Task InicializarBaseAsync(
        this IServiceProvider servicios, CancellationToken ct = default)
    {
        using var alcance = servicios.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<RepMatchDbContext>();
        var log = alcance.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ExtensionesPersistencia));
        var usarBloqueoPostgres =
            contexto.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        const int intentosMaximos = 10;

        for (var intento = 1; intento <= intentosMaximos; intento++)
        {
            try
            {
                if (usarBloqueoPostgres)
                {
                    await InicializarPostgresConBloqueoAsync(contexto, ct);
                }
                else
                {
                    await contexto.Database.EnsureCreatedAsync(ct);
                    await DatosSemilla.SembrarAsync(contexto, ct);
                }

                log.LogInformation("Base de datos lista (proveedor {Proveedor}).",
                    contexto.Database.ProviderName);
                return;
            }
            catch (Exception ex) when (intento < intentosMaximos)
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

    private static async Task InicializarPostgresConBloqueoAsync(
        RepMatchDbContext contexto, CancellationToken ct)
    {
        var baseDatos = contexto.Database;
        await baseDatos.OpenConnectionAsync(ct);
        var bloqueoAdquirido = false;

        try
        {
            await EjecutarBloqueoAdvisoryAsync(contexto, adquirir: true, ct);
            bloqueoAdquirido = true;

            await baseDatos.EnsureCreatedAsync(ct);
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
