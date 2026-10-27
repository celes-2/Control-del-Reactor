using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

public class ArenaEscenarioTests
{
    private static readonly string[] MapaChico =
    [
        "S...S",
        ".WWW.",
        "..R..",
        ".T.W.",
        "....."
    ];

    [Fact]
    public void DesdeFilas_LeeTamanoReactorMurosTrampasEInicios()
    {
        var arena = Arena.DesdeFilas(MapaChico);

        Assert.Equal(5, arena.Ancho);
        Assert.Equal(5, arena.Alto);
        Assert.Equal(new Posicion(2, 2), arena.PosicionReactor);
        Assert.Equal(2, arena.CantidadPuntosDeAparicion);
        Assert.Equal(new Posicion(0, 0), arena.PuntoDeAparicion(0));
        Assert.Equal(new Posicion(4, 0), arena.PuntoDeAparicion(1));
        Assert.Equal(TipoObstaculo.Muro, arena.ObstaculoEn(new Posicion(2, 1))?.Tipo);
        Assert.Equal(TipoObstaculo.Trampa, arena.ObstaculoEn(new Posicion(1, 3))?.Tipo);
        Assert.Null(arena.ObstaculoEn(new Posicion(0, 0)));
    }

    [Fact]
    public void DesdeFilas_LosInicios_SeOrdenanDeArribaAbajoYDeIzquierdaADerecha()
    {
        var arena = Arena.DesdeFilas(Escenarios.Predeterminado);

        Assert.Equal(4, arena.CantidadPuntosDeAparicion);
        Assert.Equal(new Posicion(1, 1), arena.PuntoDeAparicion(0));
        Assert.Equal(new Posicion(13, 1), arena.PuntoDeAparicion(1));
        Assert.Equal(new Posicion(1, 9), arena.PuntoDeAparicion(2));
        Assert.Equal(new Posicion(13, 9), arena.PuntoDeAparicion(3));
        Assert.Equal(new Posicion(7, 5), arena.PosicionReactor);
    }

    [Fact]
    public void ComoFilas_DevuelveElMismoEscenario()
    {
        Assert.Equal(MapaChico, Arena.DesdeFilas(MapaChico).ComoFilas().ToArray());
        Assert.Equal(Escenarios.Predeterminado.ToArray(), Arena.DesdeFilas(Escenarios.Predeterminado).ComoFilas().ToArray());
    }

    [Fact]
    public void DesdeFilas_FilasDeDistintoLargo_Falla()
    {
        string[] mapa = ["S...S", "..R.", ".....", ".....", "....."];

        Assert.Throws<DominioException>(() => Arena.DesdeFilas(mapa));
    }

    [Fact]
    public void DesdeFilas_ConCaracterDesconocido_Falla()
    {
        string[] mapa = ["S...S", ".X...", "..R..", ".....", "....."];

        Assert.Throws<DominioException>(() => Arena.DesdeFilas(mapa));
    }

    [Fact]
    public void DesdeFilas_SinReactorOConDos_Falla()
    {
        string[] sinReactor = ["S...S", ".....", ".....", ".....", "....."];
        string[] dosReactores = ["S...S", ".....", "R...R", ".....", "....."];

        Assert.Throws<DominioException>(() => Arena.DesdeFilas(sinReactor));
        Assert.Throws<DominioException>(() => Arena.DesdeFilas(dosReactores));
    }

    [Fact]
    public void DesdeFilas_ConMenosDeDosOMasDeCuatroInicios_Falla()
    {
        string[] unoSolo = ["S....", ".....", "..R..", ".....", "....."];
        string[] cinco = ["S.S.S", ".....", "..R..", ".....", "S...S"];

        Assert.Throws<DominioException>(() => Arena.DesdeFilas(unoSolo));
        Assert.Throws<DominioException>(() => Arena.DesdeFilas(cinco));
    }

    [Fact]
    public void DesdeFilas_ConArenaMuyPequena_Falla()
    {
        string[] mapa = ["S.S", ".R.", "..."];

        Assert.Throws<DominioException>(() => Arena.DesdeFilas(mapa));
    }

    [Fact]
    public void Partida_ConEscenario_PoneALosJugadoresEnLasS()
    {
        var arena = Arena.DesdeFilas(Escenarios.Predeterminado);
        var partida = Partida.Crear(new ConfigPartida(), arena);

        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());

        Assert.Equal(new Posicion(1, 1), ana.Posicion);
        Assert.Equal(new Posicion(13, 1), beto.Posicion);
        Assert.Equal(15, partida.Config.Ancho);
        Assert.Equal(11, partida.Config.Alto);
    }

    [Fact]
    public void Partida_ConEscenario_RespetaLosMuros()
    {
        var partida = Partida.Crear(new ConfigPartida(), Arena.DesdeFilas(Escenarios.Predeterminado));
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar();

        // Ana está en (1,1): arriba y a la izquierda hay muros del borde.
        Assert.Equal(ResultadoAccion.Bloqueado, partida.Ejecutar(ana.Id, Accion.Arriba));
        Assert.Equal(ResultadoAccion.Bloqueado, partida.Ejecutar(ana.Id, Accion.Izquierda));
        Assert.Equal(ResultadoAccion.Movido, partida.Ejecutar(ana.Id, Accion.Derecha));
    }

    [Fact]
    public void Partida_ConEscenarioDeDosInicios_LimitaMaxJugadores()
    {
        var partida = Partida.Crear(new ConfigPartida { MaxJugadores = 4 }, Arena.DesdeFilas(MapaChico));

        Assert.Equal(2, partida.Config.MaxJugadores);
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        Assert.Throws<DominioException>(() => partida.Unirse(Guid.NewGuid(), "Carla", Guid.NewGuid()));
    }
}
