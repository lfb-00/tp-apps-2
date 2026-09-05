using Microsoft.EntityFrameworkCore;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Persistence.Repositorios;

public sealed class RepuestoRepository(RepMatchDbContext contexto) : IRepuestoRepository
{
    public Task<Repuesto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        contexto.Repuestos
            .Include(r => r.Aplicaciones)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Repuesto?> ObtenerPorCodigoAsync(string codigoCanonico, CancellationToken ct = default)
    {
        var normalizado = codigoCanonico.Trim().ToUpperInvariant();
        return contexto.Repuestos
            .Include(r => r.Aplicaciones)
            .FirstOrDefaultAsync(r => r.CodigoCanonico == normalizado, ct);
    }

    public async Task<IReadOnlyList<Repuesto>> ListarAsync(CancellationToken ct = default) =>
        await contexto.Repuestos
            .Include(r => r.Aplicaciones)
            .OrderBy(r => r.CodigoCanonico)
            .ToListAsync(ct);

    /// <summary>
    /// Filtra en la base por marca, modelo y rango de anios (que es lo que el motor puede traducir
    /// a SQL con indice) y deja la regla fina del motor a la entidad, que es donde vive. Asi la
    /// consulta es eficiente sin duplicar la logica de compatibilidad en la capa de datos.
    /// </summary>
    public async Task<IReadOnlyList<Repuesto>> BuscarCompatiblesAsync(
        DatosVehiculo vehiculo, SistemaVehiculo? sistema = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);

        var consulta = contexto.Repuestos
            .Include(r => r.Aplicaciones)
            .Where(r => r.Aplicaciones.Any(a =>
                a.Marca.ToLower() == vehiculo.Marca.ToLower()
                && a.Modelo.ToLower() == vehiculo.Modelo.ToLower()
                && a.AnioDesde <= vehiculo.Anio
                && a.AnioHasta >= vehiculo.Anio));

        if (sistema is not null)
            consulta = consulta.Where(r => r.Sistema == sistema.Value);

        var candidatos = await consulta.OrderBy(r => r.CodigoCanonico).ToListAsync(ct);

        return [.. candidatos.Where(r => r.EsCompatibleCon(vehiculo))];
    }

    public async Task AgregarAsync(Repuesto repuesto, CancellationToken ct = default) =>
        await contexto.Repuestos.AddAsync(repuesto, ct);
}
