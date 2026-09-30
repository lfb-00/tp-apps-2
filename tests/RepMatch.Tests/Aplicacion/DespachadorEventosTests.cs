using Microsoft.Extensions.Logging.Abstractions;
using RepMatch.Aplicacion.Eventos;
using RepMatch.Domain.Comun;
using RepMatch.Domain.Eventos;
using RepMatch.Domain.ValueObjects;

namespace RepMatch.Tests.Aplicacion;

public class DespachadorEventosTests
{
    [Fact]
    public async Task Entrega_el_evento_solo_al_observador_que_lo_acepta()
    {
        var interesado = new ObservadorFijo(acepta: true);
        var otro = new ObservadorFijo(acepta: false);
        var despachador = new DespachadorEventos([interesado, otro], NullLogger<DespachadorEventos>.Instance);

        var evento = new BusquedaCreada(
            Guid.NewGuid(), Guid.NewGuid(), new DatosVehiculo("Volkswagen", "Gol", 2015, "1.6"),
            "cuando freno vibra el volante");

        await despachador.DespacharAsync([evento]);

        Assert.Same(evento, Assert.Single(interesado.Recibidos));
        Assert.Empty(otro.Recibidos);
    }

    private sealed class ObservadorFijo(bool acepta) : IObservadorEventoDominio
    {
        public List<IEventoDominio> Recibidos { get; } = [];

        public bool PuedeObservar(IEventoDominio evento) => acepta;

        public Task ObservarAsync(IEventoDominio evento, CancellationToken ct = default)
        {
            Recibidos.Add(evento);
            return Task.CompletedTask;
        }
    }
}
