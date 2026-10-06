using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Servicios;
using RepMatch.Aplicacion.Validadores;
using RepMatch.Contracts.Dtos;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.ValueObjects;
using RepMatch.Persistence;
using RepMatch.Persistence.Repositorios;

namespace RepMatch.Tests.Aplicacion;

/// <summary>
/// Cambio de contraseña y baja de cuenta contra repositorios reales sobre InMemory. Cada prueba
/// verifica con un contexto nuevo, para no leer lo que quedo en el change tracker.
/// </summary>
public class ServicioClientesTests
{
    private const string Contrasena = "repMatch123!";

    private readonly string nombreBase = Guid.NewGuid().ToString();

    private RepMatchDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RepMatchDbContext>().UseInMemoryDatabase(nombreBase).Options);

    private static ServicioClientes CrearServicio(RepMatchDbContext contexto) =>
        new(new ClienteRepository(contexto),
            new BusquedaRepository(contexto),
            new UnitOfWork(contexto),
            new ValidadorCrearCliente(),
            new ValidadorActualizarPerfil(),
            new ValidadorCambiarContrasena(),
            new ValidadorCambiarFotoPerfil(),
            new ValidadorEliminarCuenta(),
            NullLogger<ServicioClientes>.Instance);

    /// <summary>Cliente con dos autos (uno predeterminado) y dos busquedas, mas una busqueda de
    /// otra persona que no se tiene que tocar.</summary>
    private async Task<Guid> SembrarClienteAsync()
    {
        await using var contexto = CrearContexto();

        var cliente = new Cliente("Ana Prueba", "ana@ejemplo.com");
        cliente.EstablecerContrasena(BCrypt.Net.BCrypt.HashPassword(Contrasena));
        var gol = cliente.AgregarVehiculo(new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"));
        cliente.AgregarVehiculo(new DatosVehiculo("Peugeot", "208", 2019));
        cliente.EstablecerVehiculoPredeterminado(gol.Id);
        await contexto.Clientes.AddAsync(cliente);

        await contexto.Busquedas.AddRangeAsync(
            new Busqueda(cliente.Id, gol.Datos, "ruido al frenar en bajada"),
            new Busqueda(cliente.Id, gol.Datos, "pierde agua por abajo"),
            new Busqueda(Guid.NewGuid(), gol.Datos, "busqueda de otra persona"));

        await contexto.SaveChangesAsync();
        return cliente.Id;
    }

    [Fact]
    public async Task Cambiar_contrasena_con_la_actual_incorrecta_falla_y_no_la_cambia()
    {
        var clienteId = await SembrarClienteAsync();

        await using (var contexto = CrearContexto())
        {
            var resultado = await CrearServicio(contexto).CambiarContrasenaAsync(clienteId, new CambiarContrasenaDto
            {
                ContrasenaActual = "otraCualquiera",
                ContrasenaNueva = "nuevaClave123",
                RepetirContrasena = "nuevaClave123"
            });

            Assert.True(resultado.EsFallido);
            Assert.Equal("La contraseña actual es incorrecta.", resultado.Error);
        }

        await using (var contexto = CrearContexto())
        {
            var servicio = CrearServicio(contexto);
            Assert.NotNull(await servicio.AutenticarAsync("ana@ejemplo.com", Contrasena));
            Assert.Null(await servicio.AutenticarAsync("ana@ejemplo.com", "nuevaClave123"));
        }
    }

    [Fact]
    public async Task Cambiar_contrasena_con_la_actual_correcta_permite_entrar_con_la_nueva()
    {
        var clienteId = await SembrarClienteAsync();

        await using (var contexto = CrearContexto())
        {
            var resultado = await CrearServicio(contexto).CambiarContrasenaAsync(clienteId, new CambiarContrasenaDto
            {
                ContrasenaActual = Contrasena,
                ContrasenaNueva = "nuevaClave123",
                RepetirContrasena = "nuevaClave123"
            });

            Assert.True(resultado.EsExitoso);
        }

        await using (var contexto = CrearContexto())
        {
            Assert.NotNull(await CrearServicio(contexto).AutenticarAsync("ana@ejemplo.com", "nuevaClave123"));
        }
    }

    [Fact]
    public async Task Eliminar_cuenta_con_contrasena_incorrecta_no_borra_nada()
    {
        var clienteId = await SembrarClienteAsync();

        await using (var contexto = CrearContexto())
        {
            var resultado = await CrearServicio(contexto).EliminarCuentaAsync(
                clienteId, new EliminarCuentaDto { Contrasena = "otraCualquiera" });

            Assert.True(resultado.EsFallido);
        }

        await using (var contexto = CrearContexto())
        {
            Assert.True(await contexto.Clientes.AnyAsync(c => c.Id == clienteId));
            Assert.Equal(2, await contexto.Vehiculos.CountAsync(v => v.ClienteId == clienteId));
            Assert.Equal(2, await contexto.Busquedas.CountAsync(b => b.ClienteId == clienteId));
        }
    }

    [Fact]
    public async Task Eliminar_cuenta_con_contrasena_correcta_borra_cliente_vehiculos_y_busquedas()
    {
        var clienteId = await SembrarClienteAsync();

        await using (var contexto = CrearContexto())
        {
            var resultado = await CrearServicio(contexto).EliminarCuentaAsync(
                clienteId, new EliminarCuentaDto { Contrasena = Contrasena });

            Assert.True(resultado.EsExitoso);
        }

        await using (var contexto = CrearContexto())
        {
            Assert.False(await contexto.Clientes.AnyAsync(c => c.Id == clienteId));
            Assert.Equal(0, await contexto.Vehiculos.CountAsync(v => v.ClienteId == clienteId));
            Assert.Equal(0, await contexto.Busquedas.CountAsync(b => b.ClienteId == clienteId));
            Assert.Equal(1, await contexto.Busquedas.CountAsync());   // la de la otra persona sigue
        }
    }
}
