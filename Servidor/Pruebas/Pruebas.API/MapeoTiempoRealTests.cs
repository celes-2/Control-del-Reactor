using API.TiempoReal;
using Aplicacion.CasosDeUso;
using Dominio.Juego;
using Xunit;

namespace PruebasAPI;

public class MapeoTiempoRealTests
{
    [Theory]
    [Trait("CA", "18.1")]
    [InlineData(EstadoDelReactor.Disponible, "AVAILABLE")]
    [InlineData(EstadoDelReactor.Vacio, "EMPTY")]
    [InlineData(EstadoDelReactor.Desactivado, "DISABLED")]
    public void EstadoDelReactor_SeTraduceAlTextoDelContrato(EstadoDelReactor estado, string esperado)
    {
        Assert.Equal(esperado, estado.ATexto());
    }

    [Fact]
    [Trait("CA", "18.7")]
    public void AExtraccion_LlevaTodosLosCamposDelContrato()
    {
        var usuario = Guid.NewGuid();
        var resultado = new ResultadoEjecucion(
            Guid.NewGuid(), usuario, ResultadoAccion.EnergiaExtraida, 6, 5, 30, EstadoPartida.EnCurso, null,
            EnergiaJugador: 30, EnergiaReactor: 470, EstadoReactor: EstadoDelReactor.Disponible, PuntosGanados: 10);

        var data = resultado.AExtraccion("pico-01:42");

        Assert.Equal(usuario, data.UserId);
        Assert.Equal(10, data.Points);
        Assert.Equal(30, data.Score);
        Assert.Equal(30, data.PlayerEnergy);
        Assert.Equal(470, data.ReactorEnergy);
        Assert.Equal("AVAILABLE", data.ReactorState);
        Assert.Equal("pico-01:42", data.EventId);
    }

    [Fact]
    [Trait("CA", "18.1")]
    public void EstadoPartida_IncluyeElReactor()
    {
        var partida = Partida.Crear(new ConfigPartida());

        var estado = PartidaVista.Desde(partida).AEstadoPartida(partida.Config.DuracionSegundos);

        Assert.NotNull(estado.Reactor);
        Assert.Equal(partida.Reactor.Capacidad, estado.Reactor.Capacity);
        Assert.Equal(partida.Reactor.EnergiaDisponible, estado.Reactor.Energy);
        Assert.Equal("AVAILABLE", estado.Reactor.State);
    }
}