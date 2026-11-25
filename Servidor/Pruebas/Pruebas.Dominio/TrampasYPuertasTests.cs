using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3, US-20: obstáculos (muros, puertas automáticas y tres variantes de trampa colocadas al azar).
/// Ver Docs/arena-viva.md.
/// </summary>
public class TrampasYPuertasTests
{
    // Ana aparece en (0,0), así que la celda (0,1) queda junto a ella.
    private static readonly string[] MapaTrampa =
    [
        "S.........S",
        "T..........",
        "...........",
        "....R......",
        "..........."
    ];

    private static (Partida partida, Jugador ana) PartidaConTrampa(TipoTrampa variante, ConfigPartida? config = null)
    {
        var partida = Partida.Crear(
            config ?? new ConfigPartida(),
            [new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa, variante)]);
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar();
        return (partida, ana);
    }

    // ---------- Variantes de trampa (CA-20.5, CA-20.9) ----------

    [Fact]
    [Trait("CA", "20.9")]
    public void Trampa_TierraMovediza_CongelaLoDeLaConfiguracionYRestaLaPenalizacion()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.TierraMovediza);
        ana.SumarPuntosParaPrueba(20);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado);
        Assert.Equal(3, ana.SegundosCongelado);
        Assert.Equal(15, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Trampa_CeldaEmergente_CongelaDosSegundosMas()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.CeldaEmergente);
        ana.SumarPuntosParaPrueba(20);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(5, ana.SegundosCongelado);
        Assert.Equal(15, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Trampa_RedInmovilizadora_CongelaUnSegundoMenos()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.RedInmovilizadora);
        ana.SumarPuntosParaPrueba(20);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(2, ana.SegundosCongelado);
        Assert.Equal(15, ana.Puntaje);
    }

    [Theory]
    [InlineData(TipoTrampa.TierraMovediza)]
    [InlineData(TipoTrampa.CeldaEmergente)]
    [InlineData(TipoTrampa.RedInmovilizadora)]
    [Trait("CA", "20.5")]
    public void Trampa_TodasLasVariantesRestanCincoPuntosYNoQuitanVida(TipoTrampa variante)
    {
        var (partida, ana) = PartidaConTrampa(variante);
        ana.SumarPuntosParaPrueba(30);
        var vida = ana.Vida;

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(25, ana.Puntaje);
        Assert.Equal(vida, ana.Vida);
        Assert.False(ana.Eliminado);
        Assert.True(ana.Congelado);
    }

    [Fact]
    [Trait("CA", "20.5")]
    public void Trampa_ElPuntajeNuncaBajaDeCero()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.RedInmovilizadora);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(0, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "20.5")]
    public void Trampa_PorDefectoEsTierraMovediza_ComoAntes()
    {
        Assert.Equal(TipoTrampa.TierraMovediza, new Obstaculo(new Posicion(1, 1), TipoObstaculo.Trampa).Variante);
    }

    [Fact]
    [Trait("CA", "20.5")]
    [Trait("CA", "21.6")]
    public void Trampa_ElJugadorQuedaInmovilizadoYLuegoRecuperaElMovimiento()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.TierraMovediza);
        partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Congelado, partida.Ejecutar(ana.Id, Accion.Abajo));

        partida.Tick(3);

        Assert.Equal(ResultadoAccion.Movido, partida.Ejecutar(ana.Id, Accion.Abajo));
    }

    [Fact]
    [Trait("CA", "21.7")]
    public void Trampa_RegistraUnaNovedadDeCongelamientoConLaVariante()
    {
        var (partida, ana) = PartidaConTrampa(TipoTrampa.CeldaEmergente);

        partida.Ejecutar(ana.Id, Accion.Abajo);

        var novedad = Assert.Single(partida.TomarNovedades().OfType<JugadorCongeladoNovedad>());
        Assert.Equal(ana.Id, novedad.JugadorId);
        Assert.Equal(OrigenCongelamiento.Trampa, novedad.Origen);
        Assert.Equal(TipoTrampa.CeldaEmergente, novedad.Trampa);
        Assert.Equal(5, novedad.Segundos);
    }

    // ---------- Trampas colocadas al azar (CA-20.9) ----------

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_ConArenaViva_ColocaTresTrampasDistintas()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);

        var trampas = partida.Arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa).ToList();

        Assert.Equal(3, trampas.Count);
        Assert.Equal(3, trampas.Select(t => t.Variante).Distinct().Count());
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_SinArenaViva_NoColocaTrampasNiGuardianes()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, arenaViva: false);

        Assert.False(partida.ArenaVivaActiva);
        Assert.Empty(partida.Arena.Obstaculos);
        Assert.Empty(partida.Guardianes);
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_LasTrampasAleatoriasCaenSoloEnPisoLibre()
    {
        for (var semilla = 1; semilla <= 30; semilla++)
        {
            var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", jugadores: 4, semilla: semilla);
            var arena = partida.Arena;

            foreach (var trampa in arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa))
            {
                Assert.False(arena.EsReactor(trampa.Posicion));
                Assert.True(partida.Jugadores.All(j => j.Posicion.DistanciaManhattan(trampa.Posicion) >= 3));
                Assert.False(arena.InicioGuardianes.Contains(trampa.Posicion));
                Assert.True(arena.PuertaEn(trampa.Posicion) is null);
            }
        }
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_LasTrampasCambianSegunLaSemilla()
    {
        var disposiciones = new HashSet<string>();
        for (var semilla = 1; semilla <= 20; semilla++)
        {
            var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: semilla);
            disposiciones.Add(string.Join("|", partida.Arena.Obstaculos
                .OrderBy(o => o.Posicion.Y).ThenBy(o => o.Posicion.X)
                .Select(o => $"{o.Posicion.X},{o.Posicion.Y},{o.Variante}")));
        }

        Assert.True(disposiciones.Count > 5, "las trampas deberían cambiar de lugar entre partidas");
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_ConLaMismaSemilla_ColocaLasMismasTrampas()
    {
        string Disposicion(int semilla)
        {
            var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: semilla);
            return string.Join("|", partida.Arena.ComoFilas());
        }

        Assert.Equal(Disposicion(7), Disposicion(7));
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_ConUnaArenaSinEspacio_FallaSinEmpezarLaPartida()
    {
        // Solo queda piso libre junto al reactor o a los inicios: no hay dónde poner 3 trampas.
        string[] llena =
        [
            "SWWWS",
            "WWWWW",
            "WWRWW",
            "WWWWW",
            "WWWWW"
        ];
        var partida = Partida.Crear(new ConfigPartida(), Arena.DesdeFilas(llena));
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());

        Assert.Throws<DominioException>(() => partida.Iniciar(new Random(1)));
        Assert.Equal(EstadoPartida.Espera, partida.Estado);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(61)]
    [Trait("CA", "20.9")]
    public void Config_TrampasAleatoriasFueraDeRango_Falla(int trampas)
    {
        var config = new ConfigPartida { ArenaViva = new ReglasArenaViva { TrampasAleatorias = trampas } };

        Assert.Throws<DominioException>(() => config.Validar());
    }

    // ---------- Puertas (CA-20.1, CA-20.3, CA-20.4, CA-20.8) ----------

    private static readonly string[] MapaPuerta =
    [
        "S.........S",
        "D..........",
        "...........",
        "....R......",
        "..........."
    ];

    [Fact]
    [Trait("CA", "20.1")]
    public void DesdeFilas_LeePuertasYLugaresDeGuardian()
    {
        var arena = Arena.DesdeFilas(["SD..S", ".....", "..R..", ".....", "G...G"]);

        Assert.Single(arena.Puertas);
        Assert.Equal(TipoObstaculo.Puerta, arena.ObstaculoEn(new Posicion(1, 0))?.Tipo);
        Assert.Equal(2, arena.InicioGuardianes.Count);
        Assert.Equal(new Posicion(0, 4), arena.InicioGuardianes[0]);
    }

    [Fact]
    [Trait("CA", "20.4")]
    public void Puerta_Cerrada_BloqueaElPaso()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(MapaPuerta, arenaViva: false);
        var puerta = Assert.Single(partida.Arena.Puertas);
        Assert.False(puerta.Abierta);   // la primera puerta empieza cerrada

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Bloqueado, resultado);
        Assert.Equal(new Posicion(0, 0), ana.Posicion);
    }

    [Fact]
    [Trait("CA", "20.4")]
    public void Puerta_Abierta_PermiteElPaso()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(MapaPuerta, arenaViva: false);
        partida.Arena.PuertaEn(new Posicion(0, 1))!.ReiniciarParaPrueba(abierta: true, segundosParaCambio: 6);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.Movido, resultado);
        Assert.Equal(new Posicion(0, 1), ana.Posicion);
    }

    [Fact]
    [Trait("CA", "20.4")]
    public void Puerta_ConArenaViva_AbreYCierraSolaCadaCiclo()
    {
        var config = new ConfigPartida { ArenaViva = new ReglasArenaViva { SegundosCicloPuertas = 3 } };
        var (partida, _, _) = AyudaArenaViva.Crear(MapaPuerta, config: config);
        var puerta = partida.Arena.PuertaEn(new Posicion(0, 1))!;
        var estadoInicial = puerta.Abierta;

        partida.Tick(1);
        partida.Tick(1);
        Assert.Equal(estadoInicial, puerta.Abierta);

        partida.Tick(1);
        Assert.Equal(!estadoInicial, puerta.Abierta);

        partida.Tick(3);
        Assert.Equal(estadoInicial, puerta.Abierta);
    }

    [Fact]
    [Trait("CA", "20.8")]
    public void Puerta_NoSeCierraSobreUnJugador()
    {
        var config = new ConfigPartida { ArenaViva = new ReglasArenaViva { SegundosCicloPuertas = 3 } };
        var (partida, ana, _) = AyudaArenaViva.Crear(MapaPuerta, config: config);
        var puerta = partida.Arena.PuertaEn(new Posicion(0, 1))!;
        puerta.ReiniciarParaPrueba(abierta: true, segundosParaCambio: 3);

        partida.Ejecutar(ana.Id, Accion.Abajo);          // Ana se para en la puerta abierta
        Assert.Equal(new Posicion(0, 1), ana.Posicion);

        partida.Tick(3);                                  // le tocaba cerrarse
        Assert.True(puerta.Abierta);
        Assert.Equal(new Posicion(0, 1), ana.Posicion);

        partida.Ejecutar(ana.Id, Accion.Abajo);          // Ana sale de la puerta
        partida.Tick(1);                                  // ahora sí puede cerrarse
        Assert.False(puerta.Abierta);
    }

    [Fact]
    [Trait("CA", "20.2")]
    public void ComoFilas_MuestraLaPuertaCerradaComoMuroYLaAbiertaComoPiso()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(MapaPuerta, arenaViva: false);
        var puerta = partida.Arena.PuertaEn(new Posicion(0, 1))!;

        Assert.Equal('W', partida.Arena.ComoFilas()[1][0]);

        puerta.ReiniciarParaPrueba(abierta: true, segundosParaCambio: 6);
        Assert.Equal('.', partida.Arena.ComoFilas()[1][0]);
    }

    [Fact]
    [Trait("CA", "20.3")]
    public void Muro_SigueBloqueandoElPaso()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(["SW........S", "...........", "...........", "....R......", "..........."], arenaViva: false);

        Assert.Equal(ResultadoAccion.Bloqueado, partida.Ejecutar(ana.Id, Accion.Derecha));
    }
}

internal static class JugadorPruebaExtensiones
{
    /// <summary>Le da puntos a un jugador usando una extracción real; así no hace falta abrir el dominio.</summary>
    public static void SumarPuntosParaPrueba(this Jugador jugador, int puntos)
    {
        var metodo = typeof(Jugador).GetMethod("SumarPuntos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        metodo.Invoke(jugador, [puntos]);
    }
}

internal static class PuertaPruebaExtensiones
{
    /// <summary>Deja la puerta en un estado conocido. El método real es interno del dominio.</summary>
    public static void ReiniciarParaPrueba(this Puerta puerta, bool abierta, int segundosParaCambio)
    {
        var metodo = typeof(Puerta).GetMethod("Reiniciar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        metodo.Invoke(puerta, [abierta, segundosParaCambio]);
    }
}
