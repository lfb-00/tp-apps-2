using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence;

/// <summary>
/// Contexto de EF Core. Los mapeos viven en clases IEntityTypeConfiguration separadas para que el
/// componente de dominio no tenga ni un atributo de persistencia encima: el dominio no sabe que
/// existe una base de datos.
/// </summary>
public class RepMatchDbContext(DbContextOptions<RepMatchDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Repuesto> Repuestos => Set<Repuesto>();
    public DbSet<AplicacionVehiculo> Aplicaciones => Set<AplicacionVehiculo>();
    public DbSet<Busqueda> Busquedas => Set<Busqueda>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(RepMatchDbContext).Assembly);
        base.OnModelCreating(modelo);
    }
}
