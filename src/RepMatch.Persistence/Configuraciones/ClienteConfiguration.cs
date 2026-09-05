using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("clientes");
        b.HasKey(c => c.Id);

        b.Ignore(c => c.EventosDominio);

        b.Property(c => c.Nombre).HasMaxLength(120).IsRequired();
        b.Property(c => c.Email).HasMaxLength(200).IsRequired();
        b.Property(c => c.FechaAlta).IsRequired();

        b.HasIndex(c => c.Email).IsUnique();

        // La coleccion se expone como IReadOnlyCollection, asi que EF debe escribir el campo.
        b.HasMany(c => c.Vehiculos)
            .WithOne()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Cliente.Vehiculos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
