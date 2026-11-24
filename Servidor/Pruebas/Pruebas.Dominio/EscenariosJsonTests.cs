using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3, US-23 y US-20: escenarios definidos en JSON (Dominio/Juego/Escenarios/*.json), su validación,
/// y cómo cada escenario reparte sus trampas, puertas y puntos de energía.
/// </summary>
public class EscenariosJsonTests
{
    public static readonly string[] Ids = ["centro-investigacion", "laboratorio", "estacion", "militar"];

    private static readonly Direccion[] Direcciones =
        [Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha];

    private static string Json(string filas, int trampas = 3, string id = "prueba") => $$"""
        {
          "id": "{{id}}",
          "nombre": "Prueba",
          "descripcion": "Escenario de prueba",
          "trampasAleatorias": {{trampas}},
          "filas": [{{filas}}]
        }
        """;

    private const string FilasValidas = """
        "WWWWWWWWWWW",
        "WS...G...SW",
        "W.........W",
        "WG...R...GW",
        "W.........W",
        "WS...G...SW",
        "WWWWWWWWWWW"
        """;

    // ---------- Carga del catálogo ----------

    [Fact]
    [Trait("CA", "23.1")]
    public void Catalogo_SeCargaDeLosJsonEnSuOrden()
    {
        Assert.Equal(Ids, Escenarios.Catalogo.Select(e => e.Id).ToArray());
        Assert.All(Escenarios.Catalogo, e => Assert.Equal((30, 20), (e.Ancho, e.Alto)));
    }

    [Theory]
    [InlineData("centro-investigacion")]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.2")]
    [Trait("CA", "23.5")]
    public void Escenario_PasaTodasLasValidaciones(string id)
    {
        Assert.Empty(ValidadorEscenario.Revisar(Escenarios.Obtener(id)));
    }

    [Fact]
    [Trait("CA", "23.1")]
    public void Escenarios_TienenDistribucionesDistintas()
    {
        var lab = Escenarios.Obtener("laboratorio").CrearArena();
        var estacion = Escenarios.Obtener("estacion");
        var militar = Escenarios.Obtener("militar");

        // Laboratorio: muchas puertas. Militar: pocas puertas y la mayor cantidad de trampas.
        Assert.True(lab.Puertas.Count > militar.CrearArena().Puertas.Count);
        Assert.True(militar.TrampasAleatorias > Escenarios.Obtener("laboratorio").TrampasAleatorias);
        // Estación: marca cuellos de botella (t) donde prefiere poner las trampas.
        Assert.NotEmpty(estacion.CrearArena().CeldasParaTrampas);
        Assert.All(Ids, id => Assert.True(Escenarios.Obtener(id).CrearArena().PuntosEnergia.Count >= 6));
    }

    [Fact]
    [Trait("CA", "23.8")]
    public void Aplicar_PoneElIdYLasTrampasDelEscenarioSinTocarElResto()
    {
        var config = new ConfigPartida { CantidadGuardianes = 3, DuracionSegundos = 120 };

        var militar = Escenarios.Obtener("militar").Aplicar(config);
        var clasico = Escenarios.Obtener(null).Aplicar(config);

        Assert.Equal("militar", militar.EscenarioId);
        Assert.Equal(24, militar.ArenaViva.TrampasAleatorias);
        Assert.Equal(3, militar.CantidadGuardianes);
        Assert.Equal(120, militar.DuracionSegundos);
        Assert.Null(clasico.EscenarioId);
        Assert.Equal(3, clasico.ArenaViva.TrampasAleatorias);
    }

    // ---------- JSON y validador ----------

    [Fact]
    public void DesdeJson_ValidoDevuelveElEscenario()
    {
        var escenario = Escenarios.DesdeJson(Json(FilasValidas, trampas: 5));

        Assert.Equal("prueba", escenario.Id);
        Assert.Equal(5, escenario.TrampasAleatorias);
        Assert.Equal(2, escenario.DistanciaMinimaEntreTrampas);   // valor por defecto si no viene
    }

    [Theory]
    [InlineData("{ esto no es json")]
    [InlineData("""{ "id": "x", "nombre": "X" }""")]
    public void DesdeJson_MalFormadoOIncompleto_Falla(string json)
    {
        Assert.Throws<DominioException>(() => Escenarios.DesdeJson(json));
    }

    [Fact]
    public void DesdeJson_ConElIdReservadoDelClasico_Falla()
    {
        Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(FilasValidas, id: "clasico")));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(61)]
    [Trait("CA", "20.9")]
    public void Validador_TrampasFueraDeRango_Falla(int trampas)
    {
        Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(FilasValidas, trampas)));
    }

    [Fact]
    [Trait("CA", "23.5")]
    public void Validador_PuertaPegadaAlReactor_Falla()
    {
        const string filas = """
            "WWWWWWWWWWW",
            "WS...G...SW",
            "W.........W",
            "WG..DR...GW",
            "W.........W",
            "WS...G...SW",
            "WWWWWWWWWWW"
            """;

        var error = Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(filas)));
        Assert.Contains("pegada", error.Message);
    }

    [Fact]
    [Trait("CA", "23.5")]
    public void Validador_ReactorSoloAlcanzablePorPuertas_Falla()
    {
        const string filas = """
            "WWWWWWWWWWWWW",
            "WS....G....SW",
            "W...WWDWW...W",
            "WG..W...W..GW",
            "W...D.R.D...W",
            "W...W...W...W",
            "W...WWDWW...W",
            "WS....G....SW",
            "WWWWWWWWWWWWW"
            """;

        var error = Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(filas)));
        Assert.Contains("puertas cerradas", error.Message);
    }

    [Fact]
    [Trait("CA", "23.2")]
    public void Validador_BordeAbiertoOPocosGuardianes_Falla()
    {
        const string bordeAbierto = """
            "WWWWW.WWWWW",
            "WS...G...SW",
            "W.........W",
            "WG...R...GW",
            "W.........W",
            "WS...G...SW",
            "WWWWWWWWWWW"
            """;
        const string pocosGuardianes = """
            "WWWWWWWWWWW",
            "WS...G...SW",
            "W.........W",
            "W....R....W",
            "W.........W",
            "WS...G...SW",
            "WWWWWWWWWWW"
            """;

        Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(bordeAbierto)));
        Assert.Throws<DominioException>(() => Escenarios.DesdeJson(Json(pocosGuardianes)));
    }

    [Fact]
    public void Arena_LasMarcasEYtSonPisoYUnityNoLasRecibe()
    {
        var arena = Escenarios.Obtener("estacion").CrearArena();

        Assert.All(arena.PuntosEnergia, p => Assert.True(arena.EsTransitable(p)));
        Assert.All(arena.CeldasParaTrampas, p => Assert.True(arena.EsTransitable(p)));
        Assert.All(arena.ComoFilas(), f => Assert.Matches("^[W.SRT]+$", f));
    }

    // ---------- Trampas al iniciar (CA-20.9) ----------

    [Theory]
    [InlineData("laboratorio", 1)]
    [InlineData("laboratorio", 2)]
    [InlineData("estacion", 1)]
    [InlineData("estacion", 2)]
    [InlineData("militar", 1)]
    [InlineData("militar", 2)]
    [Trait("CA", "20.9")]
    public void Iniciar_ReparteLasTrampasDelEscenarioSinPegarlasAlReactorNiALasSalidas(string id, int semilla)
    {
        var definicion = Escenarios.Obtener(id);
        var (partida, _) = AyudaArenaViva.CrearEscenario(id, semilla: semilla);
        var arena = partida.Arena;
        var trampas = arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa).ToList();
        var inicios = Enumerable.Range(0, arena.CantidadPuntosDeAparicion).Select(arena.PuntoDeAparicion).ToList();

        Assert.Equal(definicion.TrampasAleatorias, trampas.Count);
        Assert.All(trampas, t =>
        {
            Assert.True(t.Posicion.DistanciaManhattan(arena.PosicionReactor) >= 2);
            Assert.All(inicios, i => Assert.True(i.DistanciaManhattan(t.Posicion) >= 3));
            Assert.DoesNotContain(t.Posicion, arena.PuntosEnergia);
            Assert.DoesNotContain(t.Posicion, arena.InicioGuardianes);
        });

        // Separadas: ninguna a menos de la distancia mínima de otra.
        var minima = trampas.SelectMany(a => trampas.Where(b => b != a).Select(b => a.Posicion.DistanciaManhattan(b.Posicion))).Min();
        Assert.True(minima >= definicion.DistanciaMinimaEntreTrampas);

        // Las 3 variantes, repartidas parejo.
        var porVariante = trampas.GroupBy(t => t.Variante).Select(g => g.Count()).ToList();
        Assert.Equal(3, porVariante.Count);
        Assert.True(porVariante.Max() - porVariante.Min() <= 1);
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_ConOtraSemilla_LasTrampasCambianDeLugar()
    {
        string Trampas(int semilla) => string.Join(";", AyudaArenaViva.CrearEscenario("militar", semilla: semilla).partida
            .Arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa)
            .Select(o => o.Posicion).OrderBy(p => p.Y).ThenBy(p => p.X));

        Assert.NotEqual(Trampas(1), Trampas(2));
        Assert.Equal(Trampas(3), Trampas(3));
    }

    [Fact]
    [Trait("CA", "20.9")]
    public void Iniciar_EnLaEstacion_LasTrampasVanPrimeroALosCuellosDeBotella()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("estacion", semilla: 4);
        var arena = partida.Arena;
        var trampas = arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa).Select(o => o.Posicion).ToList();

        var enCuellos = trampas.Count(arena.CeldasParaTrampas.Contains);

        Assert.True(enCuellos >= trampas.Count / 2, $"solo {enCuellos} de {trampas.Count} quedaron en celdas t");
    }

    // ---------- Puertas y energía ----------

    [Fact]
    [Trait("CA", "20.4")]
    public void Iniciar_LasPuertasNoCambianTodasAlMismoTiempo()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio");

        var momentos = partida.Arena.Puertas.Select(p => p.SegundosParaCambio).Distinct().Count();

        Assert.True(momentos > 1);
        Assert.Contains(partida.Arena.Puertas, p => p.Abierta);
        Assert.Contains(partida.Arena.Puertas, p => !p.Abierta);
    }

    [Theory]
    [InlineData("centro-investigacion")]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "22.1")]
    public void EnergiaAdicional_AparecePorLoMenosSeisVecesEnLosPuntosDelEscenario(string id)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario(id, config: new ConfigPartida { SegundosCongelamiento = 0 });
        var puntos = partida.Arena.PuntosEnergia;
        var apariciones = 0;

        for (var i = 0; i < partida.Config.DuracionSegundos && partida.Estado == EstadoPartida.EnCurso; i++)
        {
            partida.Tick(1);
            var nuevas = partida.TomarNovedades().OfType<EventoIniciadoNovedad>().Count(e => e.Tipo == TipoEvento.EnergiaExtra);
            if (nuevas > 0)
                Assert.All(partida.Items, item => Assert.Contains(item.Posicion, puntos));
            apariciones += nuevas;
        }

        Assert.True(apariciones >= 6, $"apareció {apariciones} veces");
    }
}
