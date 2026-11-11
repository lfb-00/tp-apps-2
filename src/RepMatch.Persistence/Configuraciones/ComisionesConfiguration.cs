using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepMatch.Domain.Entidades;

namespace RepMatch.Persistence.Configuraciones;

public sealed class ComisionesConfiguration : IEntityTypeConfiguration<ComisionesTienda>
{
    public void Configure(EntityTypeBuilder<ComisionesTienda> b)
    {
        b.ToTable("comisiones_tienda");
        b.HasKey(c => c.Id);

        b.Ignore(c => c.EventosDominio);

        b.Property(c => c.Tienda).HasMaxLength(80).IsRequired();
        b.Property(c => c.Periodo).HasMaxLength(7).IsRequired();

        // Una fila por tienda y mes. En Postgres, ademas, frena a dos consumidores que quisieran
        // crear la misma fila a la vez: el segundo falla, reintenta y la encuentra.
        b.HasIndex(c => new { c.Tienda, c.Periodo }).IsUnique();
    }
}

public sealed class MensajeProcesadoConfiguration : IEntityTypeConfiguration<MensajeProcesado>
{
    public void Configure(EntityTypeBuilder<MensajeProcesado> b)
    {
        b.ToTable("mensajes_procesados");

        // La clave es la idempotencia: el mismo mensaje no se puede marcar dos veces para el mismo
        // consumidor, aunque dos entregas lleguen a la vez.
        b.HasKey(m => new { m.MensajeId, m.Consumidor });
        b.Property(m => m.Consumidor).HasMaxLength(80).IsRequired();
        b.Property(m => m.ProcesadoEn).IsRequired();
    }
}
