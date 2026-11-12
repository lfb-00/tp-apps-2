using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepMatch.Persistence;

namespace RepMatch.Tests.Persistencia;

public class MigracionesTests
{
    [Fact]
    public void Las_migraciones_crean_el_esquema_completo_y_actualizan_las_tablas_de_mensajeria()
    {
        var opciones = new DbContextOptionsBuilder<RepMatchDbContext>()
            .UseNpgsql("Host=localhost;Database=repmatch;Username=repmatch")
            .Options;

        using var contexto = new RepMatchDbContext(opciones);
        var migraciones = contexto.Database.GetMigrations().ToArray();
        var script = contexto.GetService<IMigrator>().GenerateScript();

        Assert.Contains(migraciones, migration => migration.EndsWith("_EsquemaInicial", StringComparison.Ordinal));
        Assert.Contains(migraciones, migration => migration.EndsWith("_AgregarTablasMensajeria", StringComparison.Ordinal));
        Assert.Contains("CREATE TABLE clientes", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS comisiones_tienda", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS mensajes_procesados", script);
    }
}
