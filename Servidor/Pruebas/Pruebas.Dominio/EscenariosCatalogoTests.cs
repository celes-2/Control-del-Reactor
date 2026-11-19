using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3, US-23: escenarios seleccionables y arena viva sobre ellos.
/// Las reglas son iguales en todos; cambian los obstáculos, las rutas y las puertas.
/// </summary>
public class EscenariosCatalogoTests
{
    public static readonly string[] Ids = ["laboratorio", "estacion", "militar"];

    // ---------- Catálogo (CA-23.1, 23.2) ----------

    [Fact]
    [Trait("CA", "23.1")]
    public void Catalogo_TieneAlMenosDosEscenariosDistintos()
    {
        Assert.True(Escenarios.Catalogo.Count >= 2);
        Assert.Equal(Escenarios.Catalogo.Count, Escenarios.Catalogo.Select(e => e.Id).Distinct().Count());
        Assert.Equal(Escenarios.Catalogo.Count, Escenarios.Catalogo.Select(e => string.Join("", e.Filas)).Distinct().Count());
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.2")]
    public void Escenario_SeCargaYDefineLimitesInicioReactorObstaculosYGuardianes(string id)
    {
        var definicion = Escenarios.Obtener(id);
        var arena = Arena.DesdeFilas(definicion.Filas);

        Assert.Equal(30, arena.Ancho);
        Assert.Equal(20, arena.Alto);
        Assert.Equal(4, arena.CantidadPuntosDeAparicion);
        Assert.Equal(4, arena.InicioGuardianes.Count);
        Assert.NotEmpty(arena.Obstaculos);
        Assert.NotEmpty(arena.Puertas);
        Assert.False(string.IsNullOrWhiteSpace(definicion.Nombre));
        Assert.False(string.IsNullOrWhiteSpace(definicion.Descripcion));
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.5")]
    public void Escenario_LosInicioNoEstanEnObstaculosNiFueraDeLaArena(string id)
    {
        var arena = Arena.DesdeFilas(Escenarios.Obtener(id).Filas);

        for (var i = 0; i < arena.CantidadPuntosDeAparicion; i++)
        {
            var inicio = arena.PuntoDeAparicion(i);
            Assert.True(arena.EstaDentro(inicio));
            Assert.Null(arena.ObstaculoEn(inicio));
        }
        foreach (var guardian in arena.InicioGuardianes)
            Assert.Null(arena.ObstaculoEn(guardian));
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.6")]
    public void Escenario_ElReactorTieneSusCuatroVecinasLibresYTodoSeAlcanzaDesdeLosInicios(string id)
    {
        var arena = Arena.DesdeFilas(Escenarios.Obtener(id).Filas);
        var reactor = arena.PosicionReactor;

        foreach (var dir in new[] { Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha })
        {
            var vecina = reactor.Mover(dir);
            Assert.True(arena.EstaDentro(vecina) && arena.ObstaculoEn(vecina) is null);
        }

        // Alcanzable contando las puertas como pasables (se abren solas).
        var alcanzadas = Alcanzables(arena, arena.PuntoDeAparicion(0));
        for (var i = 0; i < arena.CantidadPuntosDeAparicion; i++)
            Assert.Contains(arena.PuntoDeAparicion(i), alcanzadas);
        foreach (var guardian in arena.InicioGuardianes)
            Assert.Contains(guardian, alcanzadas);
        foreach (var dir in new[] { Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha })
            Assert.Contains(reactor.Mover(dir), alcanzadas);
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.6")]
    public void Escenario_ElReactorSeAlcanzaSinDependerDeLasPuertas(string id)
    {
        // Con todas las puertas cerradas, cada jugador debe poder llegar al reactor (nadie queda encerrado).
        var arena = Arena.DesdeFilas(Escenarios.Obtener(id).Filas);
        var alcanzadas = Alcanzables(arena, arena.PuntoDeAparicion(0), puertasPasables: false);

        for (var i = 0; i < arena.CantidadPuntosDeAparicion; i++)
            Assert.Contains(arena.PuntoDeAparicion(i), alcanzadas);
        var vecinasAlcanzables = new[] { Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha }
            .Count(d => alcanzadas.Contains(arena.PosicionReactor.Mover(d)));
        Assert.True(vecinasAlcanzables >= 2, "al menos dos lados del reactor deben alcanzarse con las puertas cerradas");
    }

    private static HashSet<Posicion> Alcanzables(Arena arena, Posicion desde, bool puertasPasables = true)
    {
        var vistas = new HashSet<Posicion> { desde };
        var cola = new Queue<Posicion>([desde]);
        while (cola.Count > 0)
        {
            var actual = cola.Dequeue();
            foreach (var dir in new[] { Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha })
            {
                var vecina = actual.Mover(dir);
                if (vistas.Contains(vecina) || !arena.EstaDentro(vecina) || arena.EsReactor(vecina))
                    continue;
                var obstaculo = arena.ObstaculoEn(vecina);
                var pasa = obstaculo is null || obstaculo.Tipo == TipoObstaculo.Trampa ||
                           (obstaculo.Tipo == TipoObstaculo.Puerta && puertasPasables);
                if (!pasa)
                    continue;
                vistas.Add(vecina);
                cola.Enqueue(vecina);
            }
        }
        return vistas;
    }

    // ---------- Selección (CA-23.3, 23.4, 23.8) ----------

    [Fact]
    [Trait("CA", "23.3")]
    public void Obtener_BuscaPorIdSinImportarMayusculas()
    {
        Assert.Equal("laboratorio", Escenarios.Obtener("Laboratorio").Id);
        Assert.Equal("militar", Escenarios.Obtener("  MILITAR ").Id);
    }

    [Fact]
    [Trait("CA", "23.8")]
    public void Obtener_SinIdDevuelveElMapaClasicoYUnoDesconocidoFalla()
    {
        Assert.Equal(Escenarios.Predeterminado, Escenarios.Obtener(null).Filas);
        Assert.Equal(Escenarios.Predeterminado, Escenarios.Obtener("").Filas);

        var error = Assert.Throws<DominioException>(() => Escenarios.Obtener("playa"));
        Assert.Contains("laboratorio", error.Message);
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.4")]
    public void Partida_TodosLosJugadoresVenElMismoEscenario(string id)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario(id);
        var arena = partida.Arena;

        Assert.Equal(id, partida.Config.EscenarioId);
        Assert.Equal(arena.ComoFilas(), partida.Arena.ComoFilas());
        Assert.Equal(30, partida.Config.Ancho);
        Assert.Equal(20, partida.Config.Alto);
    }

    [Theory]
    [InlineData("laboratorio", 2)]
    [InlineData("estacion", 3)]
    [InlineData("militar", 4)]
    [Trait("CA", "23.5")]
    public void Partida_LosJugadoresAparecenEnLosInicioDelEscenario(string id, int jugadores)
    {
        var (partida, lista) = AyudaArenaViva.CrearEscenario(id, jugadores: jugadores);

        for (var i = 0; i < jugadores; i++)
            Assert.Equal(partida.Arena.PuntoDeAparicion(i), lista[i].Posicion);
    }

    [Fact]
    [Trait("CA", "23.7")]
    public void Partida_ElEscenarioNoCambiaDuranteLaPartida()
    {
        var (partida, lista) = AyudaArenaViva.CrearEscenario("laboratorio");
        var muros = partida.Arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Muro).ToList();

        partida.Tick(60);
        partida.Ejecutar(lista[0].Id, Accion.Derecha);

        Assert.Equal(muros, partida.Arena.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Muro).ToList());
        Assert.Equal("laboratorio", partida.Config.EscenarioId);
    }

    // ---------- Invariantes de la arena viva en todos los escenarios (CA-20.8, CA-23.6) ----------

    [Theory]
    [InlineData("laboratorio", 1)]
    [InlineData("laboratorio", 2)]
    [InlineData("estacion", 1)]
    [InlineData("estacion", 2)]
    [InlineData("militar", 1)]
    [InlineData("militar", 2)]
    [Trait("CA", "20.8")]
    public void ArenaViva_JugadoresYGuardianesNuncaQuedanEnPosicionInvalidaNiSuperpuestos(string id, int semilla)
    {
        var (partida, jugadores) = AyudaArenaViva.CrearEscenario(id, jugadores: 4, semilla: semilla);
        var azar = new Random(semilla * 100);
        var acciones = Enum.GetValues<Accion>();

        for (var segundo = 0; segundo < 240 && partida.Estado == EstadoPartida.EnCurso; segundo++)
        {
            foreach (var jugador in jugadores)
                partida.Ejecutar(jugador.Id, acciones[azar.Next(acciones.Length)]);
            partida.Tick(1);

            var ocupadas = jugadores.Select(j => j.Posicion).Concat(partida.Guardianes.Select(g => g.Posicion)).ToList();
            Assert.Equal(ocupadas.Count, ocupadas.Distinct().Count());
            Assert.All(ocupadas, p => Assert.True(partida.Arena.EsTransitable(p), $"{p} no es transitable"));
            Assert.All(partida.Items, i => Assert.True(partida.Arena.EsTransitable(i.Posicion)));
        }
    }
}
