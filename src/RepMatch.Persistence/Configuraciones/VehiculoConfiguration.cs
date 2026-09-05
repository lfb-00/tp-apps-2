using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> b)
    {
        b.ToTable("vehiculos");
        b.HasKey(v => v.Id);

        b.Ignore(v => v.EventosDominio);

        b.Property(v => v.Alias).HasMaxLength(120).IsRequired();
        b.Property(v => v.Vin).HasMaxLength(17);

        // DatosVehiculo es un value object: se aplana en columnas de la misma tabla.
        b.OwnsOne(v => v.Datos, d =>
        {
            d.Property(p => p.Marca).HasColumnName("marca").HasMaxLength(50).IsRequired();
            d.Property(p => p.Modelo).HasColumnName("modelo").HasMaxLength(50).IsRequired();
            d.Property(p => p.Anio).HasColumnName("anio").IsRequired();
            d.Property(p => p.Motor).HasColumnName("motor").HasMaxLength(50);
        });

        b.Navigation(v => v.Datos).IsRequired();
    }
}
