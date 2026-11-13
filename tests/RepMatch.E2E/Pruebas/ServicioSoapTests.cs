using RepMatch.E2E.Infraestructura;
using Xunit.Abstractions;

namespace RepMatch.E2E.Pruebas;

/// <summary>
/// El servicio SOAP visto desde afuera, como lo veria otro sistema que se integra: el WSDL se
/// descarga y describe el contrato. Que la Web lo use lo prueban todas las demas: la Web principal
/// busca los repuestos por SOAP.
/// </summary>
public sealed class ServicioSoapTests(NavegadorFixture fixture, ITestOutputHelper salida) : PruebaE2E(fixture, salida)
{
    [Fact]
    public async Task El_WSDL_se_descarga_y_describe_las_cuatro_operaciones()
    {
        var respuesta = await Pagina.APIRequest.GetAsync(
            $"{Entorno.UrlCatalogoSoap.TrimEnd('/')}/ConsultaCompatibilidad.svc?wsdl");
        Assert.Equal(200, respuesta.Status);

        var wsdl = await respuesta.TextAsync();
        Assert.Contains("urn:repmatch:catalogo:v1", wsdl);
        foreach (var operacion in new[] { "ConsultarPorVehiculo", "ObtenerPorCodigo", "Listar", "VerificarCodigo" })
            Assert.Contains($"<wsdl:operation name=\"{operacion}\"", wsdl);
    }

    [Fact]
    public async Task Una_llamada_SOAP_cruda_responde_si_la_pieza_le_va_al_vehiculo()
    {
        // Lo que mandaria cualquier cliente SOAP, sin .NET de por medio: un sobre XML por POST.
        const string sobre = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body>
                <VerificarCodigo xmlns="urn:repmatch:catalogo:v1">
                  <codigo>PAST-VW-GOL-DEL</codigo><marca>Volkswagen</marca><modelo>Gol</modelo>
                  <anio>2015</anio><motor>1.6</motor>
                </VerificarCodigo>
              </s:Body>
            </s:Envelope>
            """;

        var respuesta = await Pagina.APIRequest.PostAsync(
            $"{Entorno.UrlCatalogoSoap.TrimEnd('/')}/ConsultaCompatibilidad.svc", new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "text/xml; charset=utf-8",
                    ["SOAPAction"] = "\"urn:repmatch:catalogo:v1/ConsultaCompatibilidad/VerificarCodigo\""
                },
                Data = sobre
            });

        Assert.Equal(200, respuesta.Status);
        Assert.Contains("<VerificarCodigoResult>true</VerificarCodigoResult>", await respuesta.TextAsync());
    }
}
