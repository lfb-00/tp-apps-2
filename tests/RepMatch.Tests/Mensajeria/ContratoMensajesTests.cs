using System.Text;
using System.Text.Json;
using RepMatch.Contracts.Mensajeria;
using RepMatch.Mensajeria;

namespace RepMatch.Tests.Mensajeria;

/// <summary>El contrato JSON de los mensajes y la regla de reintento, sin broker.</summary>
public class ContratoMensajesTests
{
    private static MensajeIntegracion<DerivacionTiendaV1> Derivacion() => new()
    {
        MensajeId = Guid.NewGuid(),
        Tipo = ClavesRuteo.DerivacionTienda,
        OcurridoEn = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero),
        CorrelationId = "abc123",
        Datos = new DerivacionTiendaV1
        {
            DerivacionId = Guid.NewGuid(),
            BusquedaId = Guid.NewGuid(),
            OfertaId = Guid.NewGuid(),
            Tienda = "Easy"
        }
    };

    [Fact]
    public void Un_mensaje_ida_y_vuelta_conserva_todos_los_campos()
    {
        var original = Derivacion();

        var leido = SerializacionMensajes.Deserializar<DerivacionTiendaV1>(SerializacionMensajes.Serializar(original));

        Assert.Equal(original.MensajeId, leido.MensajeId);
        Assert.Equal("derivacion.tienda", leido.Tipo);
        Assert.Equal(1, leido.Version);
        Assert.Equal("abc123", leido.CorrelationId);
        Assert.Equal(original.Datos, leido.Datos);
    }

    [Fact]
    public void Un_campo_nuevo_que_el_consumidor_no_conoce_no_lo_rompe()
    {
        var json = Encoding.UTF8.GetString(SerializacionMensajes.Serializar(Derivacion()))
            .Replace("\"tienda\":\"Easy\"", "\"tienda\":\"Easy\",\"campoDeLaVersion2\":42");

        var leido = SerializacionMensajes.Deserializar<DerivacionTiendaV1>(Encoding.UTF8.GetBytes(json));

        Assert.Equal("Easy", leido.Datos.Tienda);
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{}")]
    [InlineData("{\"mensajeId\":\"00000000-0000-0000-0000-000000000000\",\"tipo\":\"x\",\"ocurridoEn\":\"2026-11-11T00:00:00Z\",\"datos\":{}}")]
    public void Un_mensaje_ilegible_se_reconoce_para_mandarlo_a_la_DLQ(string cuerpo)
    {
        Assert.ThrowsAny<JsonException>(() =>
            SerializacionMensajes.Deserializar<DerivacionTiendaV1>(Encoding.UTF8.GetBytes(cuerpo)));
    }

    [Theory]
    [InlineData(true, true, false, DecisionEntrega.Confirmar)]
    [InlineData(true, true, true, DecisionEntrega.Confirmar)]
    [InlineData(true, false, false, DecisionEntrega.Reintentar)]
    [InlineData(true, false, true, DecisionEntrega.ADlq)]
    [InlineData(false, false, false, DecisionEntrega.ADlq)]
    public void Reintenta_una_vez_y_despues_manda_a_la_DLQ(
        bool sePudoLeer, bool sePudoProcesar, bool yaSeReintento, DecisionEntrega esperada)
    {
        Assert.Equal(esperada, ReglaEntrega.Decidir(sePudoLeer, sePudoProcesar, yaSeReintento));
    }

    [Fact]
    public void Cada_cola_tiene_su_DLQ()
    {
        var cola = new DefinicionCola("repmatch.comisiones", [ClavesRuteo.DerivacionTienda]);

        Assert.Equal("repmatch.comisiones.dlq", cola.NombreDlq);
    }

    [Fact]
    public void La_topologia_compartida_declara_las_colas_de_todos_los_consumidores()
    {
        Assert.Equal(
            ["repmatch.comisiones", "repmatch.actividad"],
            TopologiaRabbitMq.ColasConsumidores.Select(cola => cola.Nombre));
        Assert.Contains(
            ClavesRuteo.DerivacionTienda,
            TopologiaRabbitMq.ColasConsumidores.Single(cola => cola.Nombre == "repmatch.comisiones").ClavesRuteo);
        Assert.Contains(
            "#",
            TopologiaRabbitMq.ColasConsumidores.Single(cola => cola.Nombre == "repmatch.actividad").ClavesRuteo);
    }
}
