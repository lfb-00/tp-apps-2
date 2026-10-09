using RepMatch.Contracts;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Aplicacion.Catalogo;

public sealed class CatalogoVehiculosLocal(IConfiguracionVehiculoRepository configuraciones) : ICatalogoVehiculos
{
    public async Task<IReadOnlyList<OpcionVehiculoDto>> ListarOpcionesAsync(CancellationToken ct = default)
    {
        var opciones = await configuraciones.ListarAsync(ct);
        return [.. opciones.Select(c => new OpcionVehiculoDto
        {
            Marca = c.Marca,
            Modelo = c.Modelo,
            AnioDesde = c.AnioDesde,
            AnioHasta = c.AnioHasta,
            Motor = c.Motor
        })];
    }

    public Task<bool> EsConfiguracionValidaAsync(VehiculoDto vehiculo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vehiculo);
        return configuraciones.ExisteAsync(vehiculo.Marca, vehiculo.Modelo, vehiculo.Anio, vehiculo.Motor, ct);
    }
}
