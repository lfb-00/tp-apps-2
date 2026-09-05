using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class RepuestoConfiguration : IEntityTypeConfiguration<Repuesto>
{
    public void Configure(EntityTypeBuilder<Repuesto> b)
    {
        b.ToTable("repuestos");
        b.HasKey(r => r.Id);

        b.Ignore(r => r.EventosDominio);

        b.Property(r => r.CodigoCanonico).HasMaxLength(60).IsRequired();
        b.Property(r => r.Nombre).HasMaxLength(200).IsRequired();
        b.Property(r => r.Descripcion).HasMaxLength(1000);
        b.Property(r => r.Sistema).HasConversion<string>().HasMaxLength(30).IsRequired();

        b.HasIndex(r => r.CodigoCanonico).IsUnique();

        // Coleccion de primitivos: EF Core la serializa como JSON en Postgres.
        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Repuesto.CodigosEquivalentes))
            .HasField("_codigosEquivalentes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany(r => r.Aplicaciones)
            .WithOne()
            .HasForeignKey(a => a.RepuestoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Repuesto.Aplicaciones))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class AplicacionVehiculoConfiguration : IEntityTypeConfiguration<AplicacionVehiculo>
{
    public void Configure(EntityTypeBuilder<AplicacionVehiculo> b)
    {
        b.ToTable("aplicaciones_vehiculo");
        b.HasKey(a => a.Id);

        b.Ignore(a => a.EventosDominio);

        b.Property(a => a.Marca).HasMaxLength(50).IsRequired();
        b.Property(a => a.Modelo).HasMaxLength(50).IsRequired();
        b.Property(a => a.Motor).HasMaxLength(50);
        b.Property(a => a.AnioDesde).IsRequired();
        b.Property(a => a.AnioHasta).IsRequired();

        // Indice que sostiene la consulta de compatibilidad, que es la mas caliente del sistema.
        b.HasIndex(a => new { a.Marca, a.Modelo });
    }
}
