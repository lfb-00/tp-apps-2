using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RepMatch.Domain.Entidades;
using RepMatch.Domain.Repositorios;

namespace RepMatch.Tests.Integracion;

/// <summary>
/// Requisito minimo del Integrador (issue #26): un error inesperado responde ProblemDetails con el
/// correlation id para reportarlo, y nunca la excepcion ni la traza. Se hospeda Catalogo.Api con un
/// repositorio que falla; WebApplicationFactory corre en Development, el mismo entorno de compose,
/// que es donde ASP.NET mostraria la pagina de excepciones del desarrollador si no hubiera manejador.
/// </summary>
[Collection(ColeccionBaseCompartida.Nombre)]
public class ErroresTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DetalleInterno = "detalle-interno-que-no-debe-salir";

    [Fact]
    public async Task Un_500_es_ProblemDetails_con_el_correlation_id_y_sin_detalles_internos()
    {
        var http = fabrica
            .WithWebHostBuilder(b => b.ConfigureServices(s => s.AddScoped<IClienteRepository, RepositorioRoto>()))
            .CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/clientes");
        pedido.Headers.Add("X-Correlation-Id", "prueba-error-500");

        using var respuesta = await http.SendAsync(pedido);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("prueba-error-500", respuesta.Headers.GetValues("X-Correlation-Id").Single());
        using var json = JsonDocument.Parse(cuerpo);
        Assert.Equal("prueba-error-500", json.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain(DetalleInterno, cuerpo);
        Assert.DoesNotContain("InvalidOperationException", cuerpo);
        Assert.DoesNotContain(" at ", cuerpo);
    }

    private sealed class RepositorioRoto : IClienteRepository
    {
        private static InvalidOperationException Falla() => new(DetalleInterno);

        public Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => throw Falla();
        public Task<Cliente?> ObtenerPorEmailAsync(string email, CancellationToken ct = default) => throw Falla();
        public Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default) => throw Falla();
        public Task<IReadOnlyList<Cliente>> ListarMecanicosAsync(string? provincia = null, CancellationToken ct = default) => throw Falla();
        public Task AgregarAsync(Cliente cliente, CancellationToken ct = default) => throw Falla();
        public void Eliminar(Cliente cliente) => throw Falla();
    }
}
