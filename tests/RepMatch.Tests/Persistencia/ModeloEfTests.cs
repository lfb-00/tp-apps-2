using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;

namespace RepMatch.Tests.Persistencia;

/// <summary>
/// Verifica que el mapeo de EF Core soporte las construcciones del dominio: value objects
/// aplanados (DatosVehiculo, Dinero), colecciones privadas expuestas como solo lectura y
/// colecciones de primitivos. Un error de modelo solo aparece en runtime, asi que se testea.
/// </summary>
public class ModeloEfTests
{
    private static RepMatchDbContext CrearContexto(string nombreBase)
    {
        var opciones = new DbContextOptionsBuilder<RepMatchDbContext>()
            .UseInMemoryDatabase(nombreBase)
            .Options;
        return new RepMatchDbContext(opciones);
    }

    [Fact]
    public void El_modelo_se_construye_sin_errores()
    {
        using var contexto = CrearContexto(Guid.NewGuid().ToString());

        var modelo = contexto.Model;

        Assert.NotNull(modelo.FindEntityType(typeof(RepMatch.Domain.Entidades.Cliente)));
        Assert.NotNull(modelo.FindEntityType(typeof(RepMatch.Domain.Entidades.Repuesto)));
        Assert.NotNull(modelo.FindEntityType(typeof(RepMatch.Domain.Entidades.Busqueda)));
        Assert.NotNull(modelo.FindEntityType(typeof(RepMatch.Domain.Entidades.Oferta)));
    }

    [Fact]
    public async Task Persiste_y_recupera_un_repuesto_con_aplicaciones_y_equivalencias()
    {
        var nombreBase = Guid.NewGuid().ToString();

        await using (var contexto = CrearContexto(nombreBase))
        {
            await DatosSemilla.SembrarAsync(contexto);
        }

        await using (var contexto = CrearContexto(nombreBase))
        {
            var repuesto = await contexto.Repuestos
                .Include(r => r.Aplicaciones)
                .SingleAsync(r => r.CodigoCanonico == "PAST-VW-GOL-DEL");

            Assert.Equal("Pastillas de freno delanteras", repuesto.Nombre);
            Assert.Equal(2, repuesto.Aplicaciones.Count);
            Assert.Contains("5U0698151", repuesto.CodigosEquivalentes);
            Assert.True(repuesto.EsCompatibleCon(new DatosVehiculo("Volkswagen", "Gol", 2015)));
        }
    }

    [Fact]
    public async Task Persiste_un_cliente_con_su_vehiculo_como_value_object()
    {
        var nombreBase = Guid.NewGuid().ToString();

        await using (var contexto = CrearContexto(nombreBase))
        {
            await DatosSemilla.SembrarAsync(contexto);
        }

        await using (var contexto = CrearContexto(nombreBase))
        {
            var cliente = await contexto.Clientes
                .Include(c => c.Vehiculos)
                .SingleAsync(c => c.Email == "blofaro@uade.edu.ar");

            Assert.Equal(2, cliente.Vehiculos.Count);

            var gol = cliente.Vehiculos.Single(v => v.Alias == "El Gol");
            Assert.Equal("Volkswagen", gol.Datos.Marca);
            Assert.Equal(2015, gol.Datos.Anio);
            Assert.Equal("1.6", gol.Datos.Motor);
        }
    }

    [Fact]
    public async Task Persiste_una_busqueda_con_ofertas_y_dinero()
    {
        var nombreBase = Guid.NewGuid().ToString();
        var clienteId = Guid.NewGuid();

        await using (var contexto = CrearContexto(nombreBase))
        {
            var busqueda = new RepMatch.Domain.Entidades.Busqueda(
                clienteId,
                new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
                "cuando freno vibra el volante");

            busqueda.AsignarCodigosObjetivo(["PAST-VW-GOL-DEL", "DISC-VW-GOL-256"]);
            busqueda.RegistrarOfertas(
            [
                new RepMatch.Domain.Entidades.Oferta(
                    busqueda.Id, "PAST-VW-GOL-DEL", "easy.com.ar", "Pastillas Bosch",
                    Dinero.Pesos(45000m), "https://www.easy.com.ar/p/123",
                    costoEnvio: Dinero.Pesos(5000m))
            ]);

            await contexto.Busquedas.AddAsync(busqueda);
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = CrearContexto(nombreBase))
        {
            var busqueda = await contexto.Busquedas
                .Include(b => b.Ofertas)
                .SingleAsync(b => b.ClienteId == clienteId);

            Assert.Equal(2, busqueda.CodigosObjetivo.Count);
            Assert.Equal("cuando freno vibra el volante", busqueda.TextoLibre);

            var oferta = Assert.Single(busqueda.Ofertas);
            Assert.Equal(45000m, oferta.Precio.Monto);
            Assert.Equal("ARS", oferta.Precio.Moneda);
            Assert.Equal(50000m, oferta.PrecioTotal.Monto);
        }
    }
}
