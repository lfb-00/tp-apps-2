using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class BusquedaConfiguration : IEntityTypeConfiguration<Busqueda>
{
    public void Configure(EntityTypeBuilder<Busqueda> b)
    {
        b.ToTable("busquedas");
        b.HasKey(x => x.Id);

        b.Ignore(x => x.EventosDominio);

        b.Property(x => x.TextoLibre).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.FechaCreacion).IsRequired();
        b.Property(x => x.MotivoFalla).HasMaxLength(500);

        // Snapshot del vehiculo al momento de la busqueda, aplanado en la misma tabla.
        b.OwnsOne(x => x.Vehiculo, d =>
        {
            d.Property(p => p.Marca).HasColumnName("marca").HasMaxLength(50).IsRequired();
            d.Property(p => p.Modelo).HasColumnName("modelo").HasMaxLength(50).IsRequired();
            d.Property(p => p.Anio).HasColumnName("anio").IsRequired();
            d.Property(p => p.Motor).HasColumnName("motor").HasMaxLength(50);
        });

        b.Navigation(x => x.Vehiculo).IsRequired();

        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Busqueda.CodigosObjetivo))
            .HasField("_codigosObjetivo")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany(x => x.Ofertas)
            .WithOne()
            .HasForeignKey(o => o.BusquedaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Busqueda.Ofertas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => x.ClienteId);
        b.HasIndex(x => x.FechaCreacion);
    }
}

public sealed class OfertaConfiguration : IEntityTypeConfiguration<Oferta>
{
    public void Configure(EntityTypeBuilder<Oferta> b)
    {
        b.ToTable("ofertas");
        b.HasKey(o => o.Id);

        b.Ignore(o => o.EventosDominio);
        b.Ignore(o => o.PrecioTotal);   // se calcula, no se persiste

        b.Property(o => o.CodigoRepuesto).HasMaxLength(60).IsRequired();
        b.Property(o => o.NombreTienda).HasMaxLength(80).IsRequired();
        b.Property(o => o.Titulo).HasMaxLength(300).IsRequired();
        b.Property(o => o.UrlOriginal).HasMaxLength(1000).IsRequired();
        b.Property(o => o.Disponible).IsRequired();
        b.Property(o => o.CapturadaEn).IsRequired();

        b.OwnsOne(o => o.Precio, d =>
        {
            d.Property(p => p.Monto).HasColumnName("precio_monto").HasPrecision(18, 2).IsRequired();
            d.Property(p => p.Moneda).HasColumnName("precio_moneda").HasMaxLength(3).IsRequired();
        });

        b.Navigation(o => o.Precio).IsRequired();

        b.OwnsOne(o => o.CostoEnvio, d =>
        {
            d.Property(p => p.Monto).HasColumnName("envio_monto").HasPrecision(18, 2);
            d.Property(p => p.Moneda).HasColumnName("envio_moneda").HasMaxLength(3);
        });
    }
}
