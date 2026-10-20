using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

public class PartidaTiempoTests
{
    private static (Partida partida, Jugador ana, Jugador beto) CrearPartida(ConfigPartida? config = null, bool iniciar = true)
    {
        var partida = Partida.Crear(config ?? new ConfigPartida());
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        if (iniciar)
            partida.Iniciar();
        return (partida, ana, beto);
    }

    [Fact]
    public void Tick_SumaLosSegundosTranscurridos()
    {
        var (partida, _, _) = CrearPartida();

        partida.Tick(1);
        partida.Tick(4);

        Assert.Equal(5, partida.SegundosTranscurridos);
        Assert.Equal(partida.Config.DuracionSegundos - 5, partida.SegundosRestantes);
    }

    [Fact]
    public void Tick_EnModoPuntos_CuentaTranscurridosYNoDescuentaElTiempoRestante()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 10 };
        var (partida, _, _) = CrearPartida(config);

        partida.Tick(30);

        Assert.Equal(30, partida.SegundosTranscurridos);
        Assert.Equal(config.DuracionSegundos, partida.SegundosRestantes);
    }

    [Fact]
    public void Tick_AntesDeIniciar_NoSumaTiempo()
    {
        var (partida, _, _) = CrearPartida(iniciar: false);

        partida.Tick(10);

        Assert.Equal(0, partida.SegundosTranscurridos);
    }

    [Fact]
    public void Tick_DespuesDeFinalizar_NoSigueSumando()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });
        partida.Ejecutar(ana.Id, Accion.Abajo);
        partida.Tick(30); // gana Ana

        partida.Tick(5);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(30, partida.SegundosTranscurridos);
    }

    [Fact]
    public void Prorroga_NoReiniciaLosSegundosTranscurridos()
    {
        var (partida, _, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });

        partida.Tick(30); // 0 a 0: empate, prórroga
        partida.Tick(10);

        Assert.Equal(1, partida.Prorrogas);
        Assert.Equal(40, partida.SegundosTranscurridos);
        Assert.Equal(Partida.SegundosPorEmpate - 10, partida.SegundosRestantes);
    }
}
