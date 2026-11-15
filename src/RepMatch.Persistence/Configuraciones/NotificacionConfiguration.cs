using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("notificaciones");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).ValueGeneratedNever();

        b.Ignore(n => n.EventosDominio);
        b.Ignore(n => n.Leida);

        // Texto, como Rol y Plan: la base se lee sin conocer el enum.
        b.Property(n => n.Tipo).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(n => n.CreadaEn).IsRequired();

        // Sin FK a clientes ni a busquedas, igual que derivaciones: borrar una cuenta o una consulta
        // no tiene que fallar por sus avisos; el servicio los borra explicitamente si hace falta.
        b.HasIndex(n => new { n.DestinatarioId, n.LeidaEn });
        b.HasIndex(n => new { n.DestinatarioId, n.BusquedaId });
    }
}
