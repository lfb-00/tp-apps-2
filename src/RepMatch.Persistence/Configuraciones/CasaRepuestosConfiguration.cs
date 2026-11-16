using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class CasaRepuestosConfiguration : IEntityTypeConfiguration<CasaRepuestos>
{
    public void Configure(EntityTypeBuilder<CasaRepuestos> b)
    {
        b.ToTable("casas_repuestos");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Ignore(c => c.EventosDominio);

        b.Property(c => c.IdMapa).HasMaxLength(40).IsRequired();
        b.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
        b.Property(c => c.Direccion).HasMaxLength(300);
        b.Property(c => c.Telefono).HasMaxLength(80);
        b.Property(c => c.ImportadaEn).IsRequired();

        b.HasIndex(c => c.IdMapa).IsUnique();
        // La busqueda filtra primero por un rectangulo de latitud y longitud alrededor del punto.
        b.HasIndex(c => new { c.Latitud, c.Longitud });
    }
}
