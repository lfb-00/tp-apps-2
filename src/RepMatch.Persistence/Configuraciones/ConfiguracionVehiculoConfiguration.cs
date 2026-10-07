using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class ConfiguracionVehiculoConfiguration : IEntityTypeConfiguration<ConfiguracionVehiculo>
{
    public void Configure(EntityTypeBuilder<ConfiguracionVehiculo> b)
    {
        b.ToTable("configuraciones_vehiculo");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.EventosDominio);
        b.Property(c => c.Marca).HasMaxLength(50).IsRequired();
        b.Property(c => c.Modelo).HasMaxLength(50).IsRequired();
        b.Property(c => c.Motor).HasMaxLength(50).IsRequired();
        b.Property(c => c.AnioDesde).IsRequired();
        b.Property(c => c.AnioHasta).IsRequired();
        b.HasIndex(c => new { c.Marca, c.Modelo, c.AnioDesde, c.AnioHasta, c.Motor }).IsUnique();
    }
}
