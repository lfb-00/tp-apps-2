using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;
using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// La cola de mensajes de punta a punta (issues #12, #13 y #14): la topologia existe en RabbitMQ,
/// una accion en la Web llega como mensaje hasta el worker y, con el broker caido, la app sigue
/// andando. Se omiten si el worker o RabbitMQ no responden (corrida con dotnet run sin cola).
/// </summary>
public sealed class ColaTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    private const string TextoBateria = "no arranca hace clic clic al girar la llave";
    private const string CodigoBateria = "BATE-12V-60AH";

    private static HttpClient Rabbit()
    {
        var http = new HttpClient { BaseAddress = new Uri(Entorno.UrlRabbitMq.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Entorno.UsuarioRabbitMq}:{Entorno.ContrasenaRabbitMq}")));
        return http;
    }

    private static HttpClient Worker() =>
        new() { BaseAddress = new Uri(Entorno.UrlWorker.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };

    private static ILocator Ofertas(IPage pagina) =>
        pagina.Locator($".precios[data-codigo='{CodigoBateria}'] li.oferta");

    private static async Task MostrarProbablesAsync(IPage pagina)
    {
        var detalles = pagina.Locator($".precios[data-codigo='{CodigoBateria}'] details.ofertas-probables");
        if (await detalles.CountAsync() == 0)
            return;

        if (await detalles.GetAttributeAsync("open") is null)
            await detalles.Locator("summary").ClickAsync();

        await Expect(detalles).ToHaveAttributeAsync("open", "");
    }

    private static async Task<bool> RespondeAsync(HttpClient http, string ruta)
    {
        try
        {
            return (await http.GetAsync(ruta)).IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static async Task<int> DerivacionesDeAsync(HttpClient worker, string tienda)
    {
        var filas = await worker.GetFromJsonAsync<JsonElement>("api/comisiones");
        var periodo = DateTime.UtcNow.ToString("yyyy-MM");
        return filas.EnumerateArray()
            .Where(f => f.GetProperty("tienda").GetString() == tienda && f.GetProperty("periodo").GetString() == periodo)
            .Select(f => f.GetProperty("derivaciones").GetInt32())
            .FirstOrDefault();
    }

    [SkippableFact]
    public async Task La_topologia_de_la_cola_existe_con_colas_durables_y_sus_DLQ()
    {
        Skip.If(Entorno.Escenario != Escenarios.Normal, "Corre en la fase Normal.");
        using var rabbit = Rabbit();
        Skip.IfNot(await RespondeAsync(rabbit, "api/overview"), $"RabbitMQ no responde en {Entorno.UrlRabbitMq}.");

        var exchange = await rabbit.GetFromJsonAsync<JsonElement>("api/exchanges/%2F/repmatch.eventos");
        Assert.Equal("topic", exchange.GetProperty("type").GetString());
        Assert.True(exchange.GetProperty("durable").GetBoolean());

        var colas = (await rabbit.GetFromJsonAsync<JsonElement>("api/queues/%2F")).EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c);
        foreach (var nombre in new[] { "repmatch.comisiones", "repmatch.comisiones.dlq", "repmatch.actividad", "repmatch.actividad.dlq" })
        {
            Assert.True(colas.ContainsKey(nombre), $"Falta la cola {nombre}");
            Assert.True(colas[nombre].GetProperty("durable").GetBoolean(), $"{nombre} no es durable");
        }

        Assert.Equal("repmatch.eventos.dlx",
            colas["repmatch.comisiones"].GetProperty("arguments").GetProperty("x-dead-letter-exchange").GetString());
    }

    [SkippableFact]
    public async Task Ir_a_la_tienda_llega_por_la_cola_al_registro_de_comisiones()
    {
        Skip.If(Entorno.Escenario != Escenarios.Normal, "Corre en la fase Normal.");
        using var worker = Worker();
        Skip.IfNot(await RespondeAsync(worker, "salud"), $"El worker no responde en {Entorno.UrlWorker}.");

        await ClienteConGolAsync(Pagina);
        await BuscarConSesionAsync(Pagina, TextoBateria);
        await MostrarProbablesAsync(Pagina);
        var primera = Ofertas(Pagina).First;
        await Expect(primera).ToBeVisibleAsync();
        var tienda = (await primera.Locator(".oferta-tienda").InnerTextAsync()).Split('·')[0].Trim();
        var href = await primera.GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" }).GetAttributeAsync("href");

        var antes = await DerivacionesDeAsync(worker, tienda);
        var respuesta = await Pagina.APIRequest.GetAsync(href!, new() { MaxRedirects = 0 });
        Assert.Equal(302, respuesta.Status);

        // La redireccion no espera al worker: la cuenta llega un momento despues, por la cola.
        var limite = DateTime.UtcNow.AddSeconds(20);
        var despues = antes;
        while (despues == antes && DateTime.UtcNow < limite)
        {
            await Task.Delay(500);
            despues = await DerivacionesDeAsync(worker, tienda);
        }

        Assert.Equal(antes + 1, despues);
    }

    [SkippableFact]
    [Trait("Escenario", Escenarios.SinCola)]
    public async Task Con_la_cola_caida_buscar_compartir_y_salir_a_la_tienda_siguen_andando()
    {
        Skip.IfNot(Entorno.Escenario == Escenarios.SinCola, "Corre solo con rabbitmq frenado (scripts/e2e.sh).");

        var mecanicoPagina = await NuevaPaginaAsync();
        var mecanico = await RegistrarAsync(mecanicoPagina, mecanico: true);

        await ClienteConGolAsync(Pagina);
        var id = await BuscarConSesionAsync(Pagina, TextoBateria);

        await MostrarProbablesAsync(Pagina);
        var href = await Ofertas(Pagina).First
            .GetByRole(AriaRole.Link, new() { Name = "Ir a la tienda" }).GetAttributeAsync("href");
        var respuesta = await Pagina.APIRequest.GetAsync(href!, new() { MaxRedirects = 0 });
        Assert.Equal(302, respuesta.Status);

        await CompartirAsync(Pagina, id, mecanico.Nombre);
        await Expect(Exito(Pagina)).ToContainTextAsync("Listo, se la compartiste.");
    }
}
