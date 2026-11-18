using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3, US-21: guardianes que persiguen jugadores y estados temporales (congelado).
/// La cantidad (2 a 4) se elige al crear la partida; todos se comportan igual. Ver Docs/arena-viva.md.
/// Mapa de las pruebas (AyudaArenaViva.MapaGuardianes): Ana (0,0) junto al reactor, Beto (10,0),
/// guardianes en (0,3) y (10,3).
/// </summary>
public class GuardianesTests
{
    // ---------- Cantidad y posición inicial (CA-21.1) ----------

    [Fact]
    [Trait("CA", "21.1")]
    public void Config_PorDefectoHayCuatroGuardianes()
    {
        Assert.Equal(4, new ConfigPartida().CantidadGuardianes);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [Trait("CA", "21.1")]
    public void Iniciar_CreaLaCantidadDeGuardianesElegida(int cantidad)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", guardianes: cantidad);

        Assert.Equal(cantidad, partida.Guardianes.Count);
        Assert.True(partida.Guardianes.All(g => !g.Temporal));
        Assert.Equal(cantidad, partida.Guardianes.Select(g => g.Posicion).Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [Trait("CA", "21.1")]
    public void Config_GuardianesFueraDeDosACuatro_Falla(int cantidad)
    {
        var config = new ConfigPartida { CantidadGuardianes = cantidad };

        Assert.Throws<DominioException>(() => config.Validar());
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "21.1")]
    public void Iniciar_ConCuatroGuardianes_EmpiezanEnLosCuatroExtremosDelEscenario(string escenario)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario(escenario, guardianes: 4);

        var posiciones = partida.Guardianes.Select(g => g.Posicion).ToHashSet();

        Assert.Equal(partida.Arena.InicioGuardianes.ToHashSet(), posiciones);
    }

    [Fact]
    [Trait("CA", "21.1")]
    public void Iniciar_ConDosGuardianes_EmpiezanEnExtremosOpuestos()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", guardianes: 2);

        var posiciones = partida.Guardianes.Select(g => g.Posicion).OrderBy(p => p.Y).ToList();

        Assert.Equal(new Posicion(7, 1), posiciones[0]);
        Assert.Equal(new Posicion(7, 9), posiciones[1]);
    }

    [Fact]
    [Trait("CA", "21.1")]
    public void Iniciar_SinCeldasG_LosGuardianesEmpiezanCercaDeLosBordes()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", guardianes: 4);
        var (clasica, _, _) = AyudaArenaViva.Crear(Escenarios.Predeterminado, guardianes: 4);

        Assert.Equal(4, clasica.Guardianes.Count);
        Assert.All(clasica.Guardianes, g => Assert.True(clasica.Arena.EsTransitable(g.Posicion)));
        Assert.NotEmpty(partida.Guardianes);
    }

    // ---------- Comportamiento: perseguir al más cercano (CA-21.2, CA-21.11) ----------

    [Fact]
    [Trait("CA", "21.2")]
    public void Guardian_DaUnPasoCadaDosSegundosHaciaElJugadorMasCercano()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        var guardian = partida.Guardianes[0];
        var antes = guardian.Posicion.DistanciaManhattan(ana.Posicion);

        partida.Tick(1);
        Assert.Equal(antes, guardian.Posicion.DistanciaManhattan(ana.Posicion));   // todavía no da su paso

        partida.Tick(1);
        Assert.Equal(antes - 1, guardian.Posicion.DistanciaManhattan(ana.Posicion));
    }

    [Fact]
    [Trait("CA", "21.3")]
    public void Guardian_AlAlcanzarAUnJugadorLoCongelaYSeQuedaQuieto()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);

        partida.Tick(4);   // dos pasos: (0,3) -> (0,1), junto a Ana

        Assert.True(ana.Congelado);
        Assert.Equal(3, ana.SegundosCongelado);
        Assert.Equal(4, partida.Guardianes[0].SegundosPausa);
        Assert.True(beto.Congelado);   // el otro guardián hizo lo mismo con Beto
    }

    [Fact]
    [Trait("CA", "21.9")]
    public void Guardian_AlcanzarAUnJugadorNoCambiaSuPuntaje()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        partida.Ejecutar(ana.Id, Accion.Extraer);
        var puntajeAna = ana.Puntaje;
        Assert.True(puntajeAna > 0);

        partida.Tick(4);

        Assert.True(ana.Congelado);
        Assert.Equal(puntajeAna, ana.Puntaje);
        Assert.Equal(0, beto.Puntaje);
    }

    [Fact]
    [Trait("CA", "21.11")]
    public void Guardian_SiElMasCercanoEstaCongelado_VaPorOtroJugador()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        var guardian = partida.Guardianes[0];
        Assert.Equal(3, guardian.Posicion.DistanciaManhattan(ana.Posicion));   // Ana es la más cercana
        partida.Congelar(ana.Id);

        partida.Tick(2);   // primer paso, con Ana todavía congelada

        Assert.True(ana.Congelado);
        Assert.Equal(12, guardian.Posicion.DistanciaManhattan(beto.Posicion));  // era 13: ahora va hacia Beto
    }

    [Fact]
    [Trait("CA", "21.11")]
    public void Guardian_SiTodosEstanCongelados_NoSeMueve()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        var inicial = partida.Guardianes.Select(g => g.Posicion).ToList();
        partida.Congelar(ana.Id);
        partida.Congelar(beto.Id);

        partida.Tick(2);

        Assert.Equal(inicial, partida.Guardianes.Select(g => g.Posicion).ToList());
    }

    [Fact]
    [Trait("CA", "21.2")]
    public void Guardian_NoAtraviesaMuros()
    {
        string[] mapa =
        [
            "S.........S",
            "WWWWWWWWWWW",
            "G....R....G",
            "...........",
            "..........."
        ];
        var (partida, _, _) = AyudaArenaViva.Crear(mapa);
        var inicial = partida.Guardianes.Select(g => g.Posicion).ToList();

        partida.Tick(10);

        Assert.Equal(inicial, partida.Guardianes.Select(g => g.Posicion).ToList());
    }

    [Fact]
    [Trait("CA", "21.2")]
    public void Guardian_BloqueaElPasoDeLosJugadores()
    {
        var config = new ConfigPartida { SegundosCongelamiento = 0 };
        string[] mapa =
        [
            "S.........S",
            "...........",
            "G.........G",
            "....R......",
            "..........."
        ];
        var (partida, ana, _) = AyudaArenaViva.Crear(mapa, config: config);

        Assert.Equal(ResultadoAccion.Movido, partida.Ejecutar(ana.Id, Accion.Abajo));      // (0,1): junto al guardián
        Assert.Equal(ResultadoAccion.Bloqueado, partida.Ejecutar(ana.Id, Accion.Abajo));   // (0,2): lo ocupa el guardián
        Assert.Equal(new Posicion(0, 1), ana.Posicion);
    }

    // ---------- Estados del jugador (CA-21.4 a CA-21.8) ----------

    [Fact]
    [Trait("CA", "21.5")]
    [Trait("CA", "21.4")]
    [Trait("CA", "21.6")]
    public void Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        partida.Tick(4);
        Assert.True(ana.Congelado);

        Assert.Equal(ResultadoAccion.Congelado, partida.Ejecutar(ana.Id, Accion.Abajo));
        Assert.Equal(ResultadoAccion.Congelado, partida.Ejecutar(ana.Id, Accion.Extraer));

        partida.Tick(3);   // se acaba el congelamiento (el guardián sigue quieto 1 segundo más)

        Assert.False(ana.Congelado);
        Assert.Equal(ResultadoAccion.EnergiaExtraida, partida.Ejecutar(ana.Id, Accion.Extraer));
    }

    [Fact]
    [Trait("CA", "21.7")]
    public void Guardian_AlAlcanzarARegistraUnaNovedadParaTodos()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        partida.TomarNovedades();

        partida.Tick(4);

        var novedad = partida.TomarNovedades().OfType<JugadorCongeladoNovedad>().First(n => n.JugadorId == ana.Id);
        Assert.Equal(OrigenCongelamiento.Guardian, novedad.Origen);
        Assert.Null(novedad.Trampa);
        Assert.Equal(3, novedad.Segundos);
        Assert.Empty(partida.TomarNovedades());   // ya se entregaron
    }

    [Fact]
    [Trait("CA", "21.10")]
    public void Guardian_AlAlcanzarAUnJugadorYaInmovilizado_LeSumaTiempo()
    {
        // La trampa de (0,1) inmoviliza a Ana 3 s y justo ahí hay un guardián en (0,2).
        string[] mapa =
        [
            "S.........S",
            "T..........",
            "G.........G",
            "....R......",
            "..........."
        ];
        var (partida, ana, _) = AyudaArenaViva.Crear(mapa);

        var resultado = partida.Ejecutar(ana.Id, Accion.Abajo);

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado);
        Assert.Equal(6, ana.SegundosCongelado);   // 3 de la trampa + 3 del guardián
        var congelamientos = partida.TomarNovedades().OfType<JugadorCongeladoNovedad>().ToList();
        Assert.Equal(new[] { OrigenCongelamiento.Trampa, OrigenCongelamiento.Guardian }, congelamientos.Select(c => c.Origen).ToArray());
    }

    [Fact]
    [Trait("CA", "21.10")]
    public void Guardian_DespuesDeAlcanzarAUnJugador_NoLoCongelaDeNuevoMientrasEstaQuieto()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        partida.Tick(4);
        Assert.Equal(3, ana.SegundosCongelado);

        partida.Tick(1);

        Assert.Equal(2, ana.SegundosCongelado);   // solo bajó: el guardián en pausa no la vuelve a congelar
    }

    [Fact]
    [Trait("CA", "21.8")]
    public void Jugador_NoPuedeCongelarseNiDescongelarseDesdeFueraDelDominio()
    {
        var metodos = typeof(Jugador).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name);

        Assert.DoesNotContain("Congelar", metodos);
        Assert.DoesNotContain("AvanzarTiempo", metodos);
        Assert.DoesNotContain("ExtenderCongelamiento", metodos);
    }
}
