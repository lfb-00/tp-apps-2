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
        IdsAsignadosPorElDominio(modelo);
        base.OnModelCreating(modelo);
    }

    /// <summary>
    /// <see cref="RepMatch.Domain.Comun.EntidadBase"/> asigna el Id en el constructor, asi el
    /// dominio puede referenciar una entidad (por ejemplo en <c>BusquedaCreada</c>) antes de
    /// guardarla. Por convencion EF Core supone que una clave Guid la genera el, y entonces trata
    /// como existente a todo hijo que llega con el Id ya cargado: al agregar un vehiculo a un
    /// cliente ya guardado, o una oferta a una busqueda ya guardada, emite UPDATE en vez de INSERT
    /// y falla con DbUpdateConcurrencyException. Se aplica a todas las entidades del dominio, y no
    /// en cada configuracion, para que una entidad nueva no repita el error.
    /// </summary>
    private static void IdsAsignadosPorElDominio(ModelBuilder modelo)
    {
        var entidades = modelo.Model.GetEntityTypes()
            .Where(t => typeof(RepMatch.Domain.Comun.EntidadBase).IsAssignableFrom(t.ClrType));

        foreach (var entidad in entidades)
        {
            modelo.Entity(entidad.ClrType)
                .Property(nameof(RepMatch.Domain.Comun.EntidadBase.Id))
                .ValueGeneratedNever();
        }
    }
}
