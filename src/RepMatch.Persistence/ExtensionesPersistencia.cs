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
        servicios.AddScoped<IBusquedaRepository, BusquedaRepository>();
        servicios.AddScoped<IUnitOfWork, UnitOfWork>();

        return servicios;
    }

    /// <summary>
    /// Crea el esquema si hace falta y siembra el catalogo. Con Postgres reintenta: en
    /// docker compose la API suele arrancar antes de que la base acepte conexiones.
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
                await contexto.Database.EnsureCreatedAsync(ct);
                await DatosSemilla.SembrarAsync(contexto, ct);

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
}
