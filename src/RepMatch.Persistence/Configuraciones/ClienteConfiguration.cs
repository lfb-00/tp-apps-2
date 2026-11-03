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
        b.Property(c => c.HashContrasena).HasMaxLength(200).IsRequired(false);
        b.Property(c => c.FechaAlta).IsRequired();
        b.Property(c => c.Telefono).HasMaxLength(20);
        b.Property(c => c.Provincia).HasMaxLength(60);
        b.Property(c => c.Localidad).HasMaxLength(100);
        b.Property(c => c.FotoPerfil).HasMaxLength(Cliente.TamanoMaximoFotoBytes);
        b.Property(c => c.FotoTipoContenido).HasMaxLength(20);
        b.Property(c => c.TemaPreferido).HasMaxLength(10);

        // Rol y plan como texto: la tabla se lee sin conocer los valores de las enumeraciones.
        b.Property(c => c.Rol).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(c => c.Plan).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.NombreTaller).HasMaxLength(Cliente.LongitudMaximaNombreTaller);

        b.PrimitiveCollection<IReadOnlyCollection<SistemaVehiculo>>(nameof(Cliente.Especialidades))
            .HasField("_especialidades")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(c => c.Email).IsUnique();
        b.HasIndex(c => c.Rol);

        // La coleccion se expone como IReadOnlyCollection, asi que EF debe escribir el campo.
        b.HasMany(c => c.Vehiculos)
            .WithOne()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Cliente.Vehiculos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Sin navegacion: el dominio solo guarda el Id. Si el vehiculo se borra por fuera del
        // agregado, la base deja al cliente sin predeterminado en vez de fallar.
        b.HasOne<Vehiculo>()
            .WithMany()
            .HasForeignKey(c => c.VehiculoPredeterminadoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
