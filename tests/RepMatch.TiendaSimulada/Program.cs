using System.Globalization;
using System.Text;

// Simulador de tiendas VTEX para las pruebas de punta a punta.
//
// Las pruebas no pueden depender del stock y los precios reales de easy.com.ar y fravega.com:
// cambian todos los dias. Este host responde lo mismo que el buscador de VTEX
// (/api/catalog_system/pub/products/search) con respuestas reales grabadas en
// tests/fixtures/vtex, una carpeta por tienda en la ruta: /easy/api/... y /fravega/api/...
//
//   dotnet run --project tests/RepMatch.TiendaSimulada --urls http://localhost:5084
//   Ofertas__Tiendas__0__UrlBase=http://localhost:5084/easy
//
// Una tienda que no tiene archivo grabado (por ejemplo /caida) responde 503: sirve para probar
// que una tienda caida no tumba la busqueda.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var carpeta = Path.Combine(AppContext.BaseDirectory, "fixtures");

app.MapGet("/salud", () => Results.Ok(new { servicio = "RepMatch.TiendaSimulada", estado = "ok" }));

app.MapGet("/{tienda}/api/catalog_system/pub/products/search", (string tienda, string? ft, HttpContext http) =>
{
    var grabadas = Directory.GetFiles(carpeta, $"{tienda.ToLowerInvariant()}-*.json");
    if (grabadas.Length == 0)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    // El archivo se elige por la primera palabra del termino: "Bateria 12V 60Ah" -> *-bateria.json.
    var palabra = Normalizar(ft ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    var archivo = grabadas.FirstOrDefault(a => Path.GetFileNameWithoutExtension(a).EndsWith($"-{palabra}", StringComparison.Ordinal));
    if (archivo is null)
        return Results.Content("[]", "application/json");

    // Como VTEX: 206 cuando hay mas resultados que la pagina, con el rango en la cabecera.
    http.Response.Headers["resources"] = "0-9/39";
    return Results.Text(File.ReadAllText(archivo), "application/json", Encoding.UTF8, StatusCodes.Status206PartialContent);
});

app.Run();

static string Normalizar(string texto)
{
    var limpio = new StringBuilder(texto.Length);
    foreach (var c in texto.Normalize(NormalizationForm.FormD))
    {
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            limpio.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
    }

    return limpio.ToString();
}
