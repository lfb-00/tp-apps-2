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

        b.Property(x => x.TextoLibre).HasMaxLength(Busqueda.LongitudMaximaTexto).IsRequired();
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

        // Diagnostico de la IA. Texto y no entero, igual que Estado: se lee en la base sin tabla de codigos.
        b.Property(x => x.SistemaDiagnosticado).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Urgencia).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.FuenteDiagnostico).HasMaxLength(30);

        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Busqueda.CausasProbables))
            .HasField("_causasProbables")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Busqueda.CodigosObjetivo))
            .HasField("_codigosObjetivo")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Revision del mecanico. MecanicoId no tiene FK a clientes, igual que ClienteId: si el
        // mecanico borra su cuenta, la revision queda como historial del cliente.
        b.Property(x => x.NotaMecanico).HasMaxLength(Busqueda.LongitudMaximaNota);

        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Busqueda.CodigosRecomendados))
            .HasField("_codigosRecomendados")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Precios: cuando se consultaron por ultima vez y para que piezas, para no volver a
        // preguntarle a las tiendas cada vez que se abre la busqueda.
        b.Property(x => x.PreciosConsultadosEn);

        b.PrimitiveCollection<IReadOnlyCollection<string>>(nameof(Busqueda.CodigosConPrecios))
            .HasField("_codigosConPrecios")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany(x => x.Ofertas)
            .WithOne()
            .HasForeignKey(o => o.BusquedaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Busqueda.Ofertas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => x.ClienteId);
        b.HasIndex(x => x.MecanicoId);
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
        b.Property(o => o.Coincidencia).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(o => o.Marca).HasMaxLength(80);
        b.Property(o => o.Ean).HasMaxLength(20);
        b.Property(o => o.ReferenciaTienda).HasMaxLength(80);
        b.Property(o => o.Vendedor).HasMaxLength(120);

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

        b.OwnsOne(o => o.PrecioLista, d =>
        {
            d.Property(p => p.Monto).HasColumnName("precio_lista_monto").HasPrecision(18, 2);
            d.Property(p => p.Moneda).HasColumnName("precio_lista_moneda").HasMaxLength(3);
        });

        b.HasIndex(o => o.Ean);
    }
}
