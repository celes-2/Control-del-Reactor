using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

public class ArenaYReactorTests
{
    [Fact]
    public void Posicion_Mover_CambiaLasCoordenadas()
    {
        var origen = new Posicion(3, 3);

        Assert.Equal(new Posicion(3, 2), origen.Mover(Direccion.Arriba));
        Assert.Equal(new Posicion(3, 4), origen.Mover(Direccion.Abajo));
        Assert.Equal(new Posicion(2, 3), origen.Mover(Direccion.Izquierda));
        Assert.Equal(new Posicion(4, 3), origen.Mover(Direccion.Derecha));
    }

    [Fact]
    public void Arena_PoneElReactorEnElCentro()
    {
        var arena = new Arena(11, 11);

        Assert.Equal(new Posicion(5, 5), arena.PosicionReactor);
    }

    [Fact]
    public void Arena_RechazaObstaculoSobreElReactor()
    {
        var obstaculo = new Obstaculo(new Posicion(5, 5), TipoObstaculo.Muro);

        Assert.Throws<DominioException>(() => new Arena(11, 11, [obstaculo]));
    }

    [Fact]
    public void Arena_RechazaObstaculoEnUnPuntoDeAparicion()
    {
        var obstaculo = new Obstaculo(new Posicion(0, 0), TipoObstaculo.Trampa);

        Assert.Throws<DominioException>(() => new Arena(11, 11, [obstaculo]));
    }

    [Fact]
    public void Arena_RechazaObstaculoFueraDeLosLimites()
    {
        var obstaculo = new Obstaculo(new Posicion(20, 2), TipoObstaculo.Muro);

        Assert.Throws<DominioException>(() => new Arena(11, 11, [obstaculo]));
    }

    [Fact]
    public void Arena_RechazaDosObstaculosEnLaMismaCelda()
    {
        var a = new Obstaculo(new Posicion(2, 2), TipoObstaculo.Muro);
        var b = new Obstaculo(new Posicion(2, 2), TipoObstaculo.Trampa);

        Assert.Throws<DominioException>(() => new Arena(11, 11, [a, b]));
    }

    [Fact]
    public void Reactor_Extraer_NoEntregaMasDeLoQueTiene()
    {
        var reactor = new Reactor(new Posicion(1, 1), 20);

        Assert.Equal(20, reactor.Extraer(30));
        Assert.Equal(0, reactor.EnergiaDisponible);
        Assert.Equal(0, reactor.Extraer(5));
    }

    [Fact]
    public void Reactor_Recargar_RespetaLaCapacidad()
    {
        var reactor = new Reactor(new Posicion(1, 1), 20);
        reactor.Extraer(5);

        reactor.Recargar(100);

        Assert.Equal(20, reactor.EnergiaDisponible);
    }

    [Fact]
    public void Reactor_EstaAdyacente_SoloEnLasCuatroCeldasVecinas()
    {
        var reactor = new Reactor(new Posicion(5, 5), 100);

        Assert.True(reactor.EstaAdyacente(new Posicion(4, 5)));
        Assert.True(reactor.EstaAdyacente(new Posicion(5, 6)));
        Assert.False(reactor.EstaAdyacente(new Posicion(4, 4))); // diagonal
        Assert.False(reactor.EstaAdyacente(new Posicion(5, 5)));
        Assert.False(reactor.EstaAdyacente(new Posicion(7, 5)));
    }
}
