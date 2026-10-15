using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

public class PartidaTests
{
    // Con la configuración por defecto (11x11): reactor en (5,5),
    // Ana aparece en (0,0) y Beto en (10,0).

    private static (Partida partida, Jugador ana, Jugador beto) CrearPartida(
        ConfigPartida? config = null,
        IEnumerable<Obstaculo>? obstaculos = null,
        bool iniciar = true)
    {
        var partida = Partida.Crear(config ?? new ConfigPartida(), obstaculos);
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        if (iniciar)
            partida.Iniciar();
        return (partida, ana, beto);
    }

    private static void Repetir(Partida partida, Jugador jugador, Accion accion, int veces)
    {
        for (var i = 0; i < veces; i++)
            partida.Ejecutar(jugador.Id, accion);
    }

    /// <summary>Lleva a Ana de (0,0) a (4,5), la celda vecina al reactor por la izquierda.</summary>
    private static void AcercarAnaAlReactor(Partida partida, Jugador ana)
    {
        Repetir(partida, ana, Accion.Derecha, 4);
        Repetir(partida, ana, Accion.Abajo, 5);
    }

    /// <summary>Lleva a Beto de (10,0) a (6,5), la celda vecina al reactor por la derecha.</summary>
    private static void AcercarBetoAlReactor(Partida partida, Jugador beto)
    {
        Repetir(partida, beto, Accion.Izquierda, 4);
        Repetir(partida, beto, Accion.Abajo, 5);
    }

    // ---------- Unirse e iniciar ----------

    [Fact]
    public void Unirse_AsignaPuntosDeAparicionEnLasEsquinas()
    {
        var (_, ana, beto) = CrearPartida(iniciar: false);

        Assert.Equal(new Posicion(0, 0), ana.Posicion);
        Assert.Equal(new Posicion(10, 0), beto.Posicion);
    }

    [Fact]
    public void Unirse_RechazaAvatarRepetido()
    {
        var partida = Partida.Crear(new ConfigPartida());
        var avatar = Guid.NewGuid();
        partida.Unirse(Guid.NewGuid(), "Ana", avatar);

        Assert.Throws<DominioException>(() => partida.Unirse(Guid.NewGuid(), "Beto", avatar));
    }

    [Fact]
    public void Unirse_RechazaUsuarioRepetido()
    {
        var partida = Partida.Crear(new ConfigPartida());
        var usuario = Guid.NewGuid();
        partida.Unirse(usuario, "Ana", Guid.NewGuid());

        Assert.Throws<DominioException>(() => partida.Unirse(usuario, "Ana", Guid.NewGuid()));
    }

    [Fact]
    public void Unirse_RechazaCuandoLaPartidaEstaLlena()
    {
        var partida = Partida.Crear(new ConfigPartida { MaxJugadores = 2 });
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());

        Assert.Throws<DominioException>(() => partida.Unirse(Guid.NewGuid(), "Carla", Guid.NewGuid()));
    }

    [Fact]
    public void Unirse_RechazaDespuesDeIniciar()
    {
        var (partida, _, _) = CrearPartida();

        Assert.Throws<DominioException>(() => partida.Unirse(Guid.NewGuid(), "Carla", Guid.NewGuid()));
    }

    [Fact]
    public void Iniciar_ConUnSoloJugador_Falla()
    {
        var partida = Partida.Crear(new ConfigPartida());
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());

        Assert.Throws<DominioException>(() => partida.Iniciar());
    }

    [Fact]
    public void Iniciar_PoneLaPartidaEnCurso()
    {
        var (partida, _, _) = CrearPartida();

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
        Assert.Equal(partida.Config.DuracionSegundos, partida.SegundosRestantes);
    }

    [Fact]
    public void Ejecutar_AntesDeIniciar_Falla()
    {
        var (partida, ana, _) = CrearPartida(iniciar: false);

        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Abajo));
    }

    // ---------- Movimiento ----------

    [Fact]
    public void Mover_DentroDeLaArena_CambiaLaPosicion()
    {
        var (partida, ana, _) = CrearPartida();

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Movido, resultado);
        Assert.Equal(new Posicion(0, 1), ana.Posicion);
    }

    [Fact]
    public void Mover_FueraDeLaArena_EsBloqueado()
    {
        var (partida, ana, _) = CrearPartida();

        var resultado = partida.Ejecutar(ana.Id, Accion.Arriba);

        Assert.Equal(ResultadoAccion.Bloqueado, resultado);
        Assert.Equal(new Posicion(0, 0), ana.Posicion);
    }

    [Fact]
    public void Mover_HaciaUnMuro_EsBloqueado()
    {
        var muro = new Obstaculo(new Posicion(1, 0), TipoObstaculo.Muro);
        var (partida, ana, _) = CrearPartida(obstaculos: [muro]);

        var resultado = partida.Ejecutar(ana.Id, Accion.Derecha);

        Assert.Equal(ResultadoAccion.Bloqueado, resultado);
        Assert.Equal(new Posicion(0, 0), ana.Posicion);
    }

    [Fact]
    public void Mover_HaciaOtroJugador_EsBloqueado()
    {
        // Arena de 5x5: Ana aparece en (0,0) y Beto en (4,0), en la misma fila.
        var (partida, ana, _) = CrearPartida(new ConfigPartida { Ancho = 5, Alto = 5 });

        Repetir(partida, ana, Accion.Derecha, 3);
        var resultado = partida.Ejecutar(ana.Id, Accion.Derecha);

        Assert.Equal(ResultadoAccion.Bloqueado, resultado);
        Assert.Equal(new Posicion(3, 0), ana.Posicion);
    }

    [Fact]
    public void Mover_HaciaElReactor_EsBloqueado()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);

        var resultado = partida.Ejecutar(ana.Id, Accion.Derecha);

        Assert.Equal(ResultadoAccion.Bloqueado, resultado);
        Assert.Equal(new Posicion(4, 5), ana.Posicion);
    }

    [Fact]
    public void Mover_ATrampa_QuitaVida()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (partida, ana, _) = CrearPartida(obstaculos: [trampa]);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado);
        Assert.Equal(new Posicion(0, 1), ana.Posicion);
        Assert.Equal(80, ana.Vida);
        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
    }

    [Fact]
    public void Trampa_QueEliminaAUnJugador_TerminaLaPartidaConElOtroComoGanador()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (partida, ana, beto) = CrearPartida(new ConfigPartida { DanoTrampa = 100 }, [trampa]);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.True(ana.Eliminado);
        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(beto.Id, partida.GanadorId);
    }

    [Fact]
    public void Ejecutar_ConUnJugadorEliminado_Falla()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var config = new ConfigPartida { DanoTrampa = 100, MaxJugadores = 3 };
        var (partida, ana, _) = CrearPartida(config, [trampa], iniciar: false);
        partida.Unirse(Guid.NewGuid(), "Carla", Guid.NewGuid());
        partida.Iniciar();

        partida.Ejecutar(ana.Id, Accion.Abajo); // Ana muere, pero quedan dos jugadores vivos

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Abajo));
    }

    // ---------- Extracción de energía ----------

    [Fact]
    public void Extraer_LejosDelReactor_NoHaceNada()
    {
        var (partida, ana, _) = CrearPartida();

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.LejosDelReactor, resultado);
        Assert.Equal(0, ana.Energia);
        Assert.Equal(partida.Config.CapacidadReactor, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    public void Extraer_JuntoAlReactor_PasaEnergiaAlJugador()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, resultado);
        Assert.Equal(10, ana.Energia);
        Assert.Equal(490, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    public void Extraer_ConElReactorVacio_DevuelveReactorVacio()
    {
        var config = new ConfigPartida { CapacidadReactor = 10, EnergiaPorExtraccion = 10, EnergiaObjetivo = 100 };
        var (partida, ana, beto) = CrearPartida(config);
        AcercarAnaAlReactor(partida, ana);
        AcercarBetoAlReactor(partida, beto);
        partida.Ejecutar(ana.Id, Accion.Extraer);

        var resultado = partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.ReactorVacio, resultado);
        Assert.Equal(0, beto.Energia);
    }

    // ---------- Tiempo ----------

    [Fact]
    public void Tick_RecargaElReactor()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);
        partida.Ejecutar(ana.Id, Accion.Extraer); // 490

        partida.Tick(1);

        Assert.Equal(495, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    public void Tick_NoRecargaMasQueLaCapacidad()
    {
        var (partida, _, _) = CrearPartida();

        partida.Tick(10);

        Assert.Equal(partida.Config.CapacidadReactor, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    public void Tick_AlAgotarseElTiempo_GanaQuienTengaMasPuntos()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 60 });
        partida.Ejecutar(ana.Id, Accion.Abajo);

        partida.Tick(60);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(ana.Id, partida.GanadorId);
    }

    [Fact]
    public void Tick_DespuesDeFinalizar_NoCambiaNada()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });
        partida.Ejecutar(ana.Id, Accion.Abajo);
        partida.Tick(30);

        partida.Tick(5);

        Assert.Equal(0, partida.SegundosRestantes);
        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
    }

    // ---------- Puntaje (US-15) ----------

    [Fact]
    public void Mover_SumaUnPunto()
    {
        var (partida, ana, beto) = CrearPartida();

        partida.Ejecutar(ana.Id, Accion.Abajo);
        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(2, ana.Puntaje);
        Assert.Equal(0, beto.Puntaje);
    }

    [Fact]
    public void Mover_Bloqueado_NoSumaPuntos()
    {
        var (partida, ana, _) = CrearPartida();

        partida.Ejecutar(ana.Id, Accion.Arriba); // fuera de la arena

        Assert.Equal(0, ana.Puntaje);
    }

    [Fact]
    public void Mover_ATrampa_SumaElPunto()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (partida, ana, _) = CrearPartida(obstaculos: [trampa]);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(1, ana.Puntaje);
    }

    // ---------- Fin por puntos (US-08, US-16) ----------

    [Fact]
    public void FinPorPuntos_GanaElPrimeroEnLlegarALaMeta()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 3 };
        var (partida, ana, beto) = CrearPartida(config);

        partida.Ejecutar(beto.Id, Accion.Abajo);
        Repetir(partida, ana, Accion.Abajo, 3);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(ana.Id, partida.GanadorId);
    }

    [Fact]
    public void FinPorPuntos_ElTiempoNoTerminaLaPartida()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 5, DuracionSegundos = 10 };
        var (partida, _, _) = CrearPartida(config);

        partida.Tick(60);

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
    }

    [Fact]
    public void FinPorTiempo_LosPuntosNoTerminanLaPartida()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Tiempo, PuntosObjetivo = 2 };
        var (partida, ana, _) = CrearPartida(config);

        Repetir(partida, ana, Accion.Abajo, 4);

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
    }

    // ---------- Empate (CA-16.4) ----------

    [Fact]
    public void Empate_AlAcabarseElTiempo_SumaDosMinutosYSigueConLosMismosPuntos()
    {
        var (partida, ana, beto) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });
        partida.Ejecutar(ana.Id, Accion.Abajo);
        partida.Ejecutar(beto.Id, Accion.Abajo);

        partida.Tick(30);

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
        Assert.Null(partida.GanadorId);
        Assert.Equal(Partida.SegundosPorEmpate, partida.SegundosRestantes);
        Assert.Equal(1, partida.Prorrogas);
        Assert.Equal(1, ana.Puntaje);
        Assert.Equal(1, beto.Puntaje);
    }

    [Fact]
    public void Empate_ElDesempateSeResuelveEnLaProrroga()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });
        partida.Tick(30); // 0 a 0: prórroga
        partida.Ejecutar(ana.Id, Accion.Abajo);

        partida.Tick(Partida.SegundosPorEmpate);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(ana.Id, partida.GanadorId);
    }

    // ---------- Congelamiento (CA-08.5) ----------

    [Fact]
    public void Congelado_NoPuedeMoverse()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { SegundosCongelamiento = 5 });
        partida.Congelar(ana.Id);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Congelado, resultado);
        Assert.Equal(new Posicion(0, 0), ana.Posicion);
        Assert.Equal(0, ana.Puntaje);
    }

    [Fact]
    public void Congelado_VuelveAMoverseCuandoPasaElTiempo()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { SegundosCongelamiento = 5 });
        partida.Congelar(ana.Id);

        partida.Tick(5);
        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Movido, resultado);
        Assert.False(ana.Congelado);
    }

    [Fact]
    public void Congelar_UnJugadorQueNoEsDeLaPartida_Falla()
    {
        var (partida, _, _) = CrearPartida();

        Assert.Throws<DominioException>(() => partida.Congelar(Guid.NewGuid()));
    }

    // ---------- Acción especial (US-14) ----------

    [Fact]
    public void Especial_DevuelveAccionRealizadaSinMoverNiPuntuar()
    {
        var (partida, ana, _) = CrearPartida();

        var resultado = partida.Ejecutar(ana.Id, Accion.Especial);

        Assert.Equal(ResultadoAccion.AccionRealizada, resultado);
        Assert.Equal(new Posicion(0, 0), ana.Posicion);
        Assert.Equal(0, ana.Puntaje);
    }

    [Fact]
    public void Especial_AntesDeIniciar_Falla()
    {
        var (partida, ana, _) = CrearPartida(iniciar: false);

        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Especial));
    }

    // ---------- Configuración ----------

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Crear_ConCantidadDeJugadoresInvalida_Falla(int maxJugadores)
    {
        Assert.Throws<DominioException>(() =>
            Partida.Crear(new ConfigPartida { MaxJugadores = maxJugadores }));
    }

    [Fact]
    public void Crear_ConArenaMuyPequena_Falla()
    {
        Assert.Throws<DominioException>(() =>
            Partida.Crear(new ConfigPartida { Ancho = 3, Alto = 3 }));
    }

    [Fact]
    public void Crear_ConPuntosObjetivoInvalido_Falla()
    {
        Assert.Throws<DominioException>(() =>
            Partida.Crear(new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 0 }));
    }

    [Fact]
    public void Crear_ConCongelamientoNegativo_Falla()
    {
        Assert.Throws<DominioException>(() =>
            Partida.Crear(new ConfigPartida { SegundosCongelamiento = -1 }));
    }
}
