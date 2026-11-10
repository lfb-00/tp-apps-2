using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class DerivacionConfiguration : IEntityTypeConfiguration<Derivacion>
{
    public void Configure(EntityTypeBuilder<Derivacion> b)
    {
        b.ToTable("derivaciones");
        b.HasKey(d => d.Id);

        b.Ignore(d => d.EventosDominio);

        b.Property(d => d.NombreTienda).HasMaxLength(80).IsRequired();
        b.Property(d => d.UrlDestino).HasMaxLength(1000).IsRequired();
        b.Property(d => d.OcurridaEn).IsRequired();

        // Sin FK a busquedas ni a clientes a proposito: la derivacion es el registro de la
        // comision y tiene que quedar aunque se borre la busqueda o la cuenta que la origino.
        b.HasIndex(d => d.BusquedaId);
        b.HasIndex(d => d.MecanicoId);
        b.HasIndex(d => d.OcurridaEn);
    }
}
