using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3: extraccion de energia (US-18), puntuacion (US-19), fin (US-25) y trampas (US-20).
/// Con la configuracion por defecto (11x11): reactor en (5,5), Ana aparece en (0,0) y Beto en (10,0).
/// Reglas: docs/reglas.md (1 punto por unidad de energia extraida, trampa = -5 puntos y congelamiento,
/// enfriamiento de 1 segundo por jugador).
/// </summary>
public class PartidaExtraccionPuntosTests
{
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

    // ---------- US-19: el movimiento ya no da puntos ----------

    [Fact]
    [Trait("CA", "19.4")]
    public void Mover_NoSumaPuntos()
    {
        var (partida, ana, beto) = CrearPartida();

        partida.Ejecutar(ana.Id, Accion.Abajo);
        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(0, ana.Puntaje);
        Assert.Equal(0, beto.Puntaje);
    }

    // ---------- US-18 / US-19: puntos por extraccion ----------

    [Fact]
    [Trait("CA", "18.4")]
    public void Extraer_JuntoAlReactor_SumaUnPuntoPorUnidadDeEnergia()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, resultado);
        Assert.Equal(10, ana.Energia);
        Assert.Equal(10, ana.Puntaje);
        Assert.Equal(490, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    [Trait("CA", "18.4")]
    public void Extraer_ConPocaEnergiaEnElReactor_SumaSoloLoQueSalio()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { CapacidadReactor = 4 });
        AcercarAnaAlReactor(partida, ana);

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, resultado);
        Assert.Equal(4, ana.Energia);
        Assert.Equal(4, ana.Puntaje);
        Assert.Equal(0, partida.Reactor.EnergiaDisponible);
    }

    // ---------- US-18 CA-18.5 / US-19 CA-19.5: rechazos no cambian el puntaje ----------

    [Fact]
    [Trait("CA", "18.5")]
    [Trait("CA", "19.5")]
    public void Extraer_LejosDelReactor_NoSumaPuntos()
    {
        var (partida, ana, _) = CrearPartida();

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.LejosDelReactor, resultado);
        Assert.Equal(0, ana.Puntaje);
        Assert.Equal(500, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    [Trait("CA", "18.5")]
    [Trait("CA", "19.5")]
    public void Extraer_ConElReactorVacio_NoCambiaElPuntaje()
    {
        var config = new ConfigPartida { CapacidadReactor = 10, RegeneracionPorSegundo = 0 };
        var (partida, ana, _) = CrearPartida(config);
        AcercarAnaAlReactor(partida, ana);
        partida.Ejecutar(ana.Id, Accion.Extraer);     // vacia el reactor: 10 puntos
        partida.Tick(1);                              // pasa el enfriamiento, sin recarga

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.ReactorVacio, resultado);
        Assert.Equal(10, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "18.5")]
    [Trait("CA", "19.5")]
    public void Extraer_ConElJugadorCongelado_NoSumaPuntos()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);
        partida.Congelar(ana.Id);

        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.Congelado, resultado);
        Assert.Equal(0, ana.Puntaje);
        Assert.Equal(500, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    [Trait("CA", "18.5")]
    [Trait("CA", "19.5")]
    public void Extraer_ConLaPartidaSinIniciar_Falla_YNoSumaPuntos()
    {
        var (partida, ana, _) = CrearPartida(iniciar: false);

        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Extraer));
        Assert.Equal(0, ana.Puntaje);
    }

    // ---------- US-18 CA-18.8: enfriamiento ----------

    [Fact]
    [Trait("CA", "18.8")]
    public void Extraer_DosVecesEnElMismoSegundo_SoloCuentaUna()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);

        var primera = partida.Ejecutar(ana.Id, Accion.Extraer);
        var segunda = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, primera);
        Assert.Equal(ResultadoAccion.Enfriamiento, segunda);
        Assert.Equal(10, ana.Puntaje);
        Assert.Equal(10, ana.Energia);
        Assert.Equal(490, partida.Reactor.EnergiaDisponible);
    }

    [Fact]
    [Trait("CA", "18.8")]
    public void Extraer_DespuesDeUnTick_SeAceptaDeNuevo()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);

        partida.Ejecutar(ana.Id, Accion.Extraer);
        partida.Tick(1);
        var resultado = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, resultado);
        Assert.Equal(20, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "18.8")]
    public void Extraer_CadaJugadorTieneSuPropioEnfriamiento()
    {
        var (partida, ana, beto) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);
        AcercarBetoAlReactor(partida, beto);

        var deAna = partida.Ejecutar(ana.Id, Accion.Extraer);
        var deBeto = partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, deAna);
        Assert.Equal(ResultadoAccion.EnergiaExtraida, deBeto);
        Assert.Equal(10, ana.Puntaje);
        Assert.Equal(10, beto.Puntaje);
    }

    // ---------- US-25: fin automatico con la puntuacion nueva ----------

    [Fact]
    [Trait("CA", "25.1")]
    public void FinPorPuntos_AlExtraer_GanaQuienLlegaALaMeta()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 10 };
        var (partida, ana, _) = CrearPartida(config);
        AcercarAnaAlReactor(partida, ana);

        partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Equal(ana.Id, partida.GanadorId);
    }

    [Fact]
    [Trait("CA", "25.3")]
    public void DespuesDeFinalizar_Extraer_Falla_YNoCambiaElPuntaje()
    {
        var config = new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 10 };
        var (partida, ana, _) = CrearPartida(config);
        AcercarAnaAlReactor(partida, ana);
        partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Extraer));
        Assert.Equal(10, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "25.6")]
    public void Empate_ConPuntosDeExtraccion_SumaDosMinutosYConservaElPuntaje()
    {
        var (partida, ana, beto) = CrearPartida(new ConfigPartida { DuracionSegundos = 20 });
        AcercarAnaAlReactor(partida, ana);
        AcercarBetoAlReactor(partida, beto);
        partida.Ejecutar(ana.Id, Accion.Extraer);
        partida.Ejecutar(beto.Id, Accion.Extraer);

        partida.Tick(20);

        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
        Assert.Equal(1, partida.Prorrogas);
        Assert.Equal(Partida.SegundosPorEmpate, partida.SegundosRestantes);
        Assert.Equal(10, ana.Puntaje);
        Assert.Equal(10, beto.Puntaje);
    }

    // ---------- US-20: trampas (ya no quitan vida: congelan y restan 5 puntos, minimo 0) ----------

    [Fact]
    [Trait("CA", "20.5")]
    public void Mover_ATrampa_SinPuntos_SoloCongela()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (partida, ana, _) = CrearPartida(obstaculos: [trampa]);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado);
        Assert.Equal(new Posicion(0, 1), ana.Posicion);
        Assert.Equal(0, ana.Puntaje);                       // nunca queda negativo
        Assert.True(ana.Congelado);
        Assert.Equal(partida.Config.SegundosCongelamiento, ana.SegundosCongelado);
        Assert.Equal(100, ana.Vida);                        // la trampa ya no quita vida
        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
    }

    [Fact]
    [Trait("CA", "20.5")]
    public void Mover_ATrampa_ConPuntos_RestaCinco()
    {
        var trampa = new Obstaculo(new Posicion(4, 6), TipoObstaculo.Trampa);
        var (partida, ana, _) = CrearPartida(obstaculos: [trampa]);
        AcercarAnaAlReactor(partida, ana);
        partida.Ejecutar(ana.Id, Accion.Extraer);           // 10 puntos

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);   // cae en (4,6)

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado);
        Assert.Equal(5, ana.Puntaje);
        Assert.True(ana.Congelado);
    }

    [Fact]
    [Trait("CA", "20.5")]
    public void Trampa_NuncaEliminaAUnJugadorNiTerminaLaPartida()
    {
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (partida, ana, _) = CrearPartida(obstaculos: [trampa]);

        for (var i = 0; i < 6; i++)
        {
            partida.Ejecutar(ana.Id, Accion.Abajo);         // cae en la trampa
            partida.Tick(partida.Config.SegundosCongelamiento);
            partida.Ejecutar(ana.Id, Accion.Arriba);        // sale
        }

        Assert.False(ana.Eliminado);
        Assert.Equal(100, ana.Vida);
        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
        Assert.True(ana.Puntaje >= 0);
    }

    // ---------- US-22: sobrecarga del reactor (API que usara Faby) ----------

    [Fact]
    [Trait("CA", "22.3")]
    public void Extraer_ConElReactorDesactivado_SeRechaza_YAlReactivarseVuelveAFuncionar()
    {
        var (partida, ana, _) = CrearPartida();
        AcercarAnaAlReactor(partida, ana);
        partida.DesactivarReactor(10);

        var durante = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.ReactorDesactivado, durante);
        Assert.Equal(0, ana.Puntaje);
        Assert.Equal(500, partida.Reactor.EnergiaDisponible);

        partida.Tick(10);
        var despues = partida.Ejecutar(ana.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, despues);
        Assert.Equal(10, ana.Puntaje);
    }
}
