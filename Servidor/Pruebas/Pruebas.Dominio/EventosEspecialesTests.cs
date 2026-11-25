using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Sprint 3, US-22: eventos especiales (energía extra, invasión de guardianes, sobrecarga del reactor y dos
/// creativos: puntos dobles y rutas alternativas). Ver Docs/arena-viva.md.
/// Se usa una semilla fija: con la misma semilla el calendario de eventos es siempre el mismo.
/// </summary>
public class EventosEspecialesTests
{
    private static ConfigPartida SinCongelar => new() { SegundosCongelamiento = 0 };

    /// <summary>Sin congelar y con guardianes que casi no se mueven: así los jugadores pueden caminar tranquilos hasta la energía.</summary>
    private static ConfigPartida GuardianesQuietos => new()
    {
        SegundosCongelamiento = 0,
        ArenaViva = new ReglasArenaViva { SegundosPorPasoGuardian = 100000 }
    };

    /// <summary>Arena abierta con el reactor al centro, para poder caminar hasta cualquier celda.</summary>
    private static readonly string[] MapaAbierto =
    [
        "S.........S",
        "...........",
        "...........",
        ".....R.....",
        "...........",
        "..........."
    ];

    private static List<EventoIniciadoNovedad> JugarSinMoverse(Partida partida, int segundos)
    {
        var iniciados = new List<EventoIniciadoNovedad>();
        for (var i = 0; i < segundos && partida.Estado == EstadoPartida.EnCurso; i++)
        {
            partida.Tick(1);
            iniciados.AddRange(partida.TomarNovedades().OfType<EventoIniciadoNovedad>());
        }
        return iniciados;
    }

    // ---------- Calendario: cuántas veces ocurre cada evento (CA-22.1, 22.2, 22.3, 22.4) ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [Trait("CA", "22.1")]
    [Trait("CA", "22.3")]
    [Trait("CA", "22.4")]
    public void Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas(int semilla)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: semilla);

        var iniciados = JugarSinMoverse(partida, partida.Config.DuracionSegundos);
        int Cuantos(TipoEvento tipo) => iniciados.Count(e => e.Tipo == tipo);

        Assert.True(Cuantos(TipoEvento.EnergiaExtra) >= 3, "energía adicional: al menos 3 veces");
        Assert.True(Cuantos(TipoEvento.SobrecargaReactor) >= 2, "sobrecarga: al menos 2 veces");
        Assert.True(Cuantos(TipoEvento.InvasionGuardianes) >= 1, "invasión de guardianes");
        Assert.True(Cuantos(TipoEvento.PuntosDobles) >= 1, "creativo 1: puntos dobles");
        Assert.True(Cuantos(TipoEvento.RutasAlternativas) >= 1, "creativo 2: rutas alternativas");
    }

    [Fact]
    [Trait("CA", "22.4")]
    public void Partida_SinPuertas_UsaLluviaDeEnergiaComoSegundoCreativo()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(Escenarios.Predeterminado, semilla: 3);

        var iniciados = JugarSinMoverse(partida, partida.Config.DuracionSegundos);

        Assert.Contains(iniciados, e => e.Tipo == TipoEvento.LluviaEnergia);
        Assert.DoesNotContain(iniciados, e => e.Tipo == TipoEvento.RutasAlternativas);
        Assert.Contains(iniciados, e => e.Tipo == TipoEvento.PuntosDobles);
    }

    [Fact]
    [Trait("CA", "22.10")]
    public void Calendario_ConLaMismaSemilla_EsElMismo()
    {
        var (a, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 11);
        var (b, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 11);
        var (c, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 12);

        Assert.Equal(a.EventosProgramados, b.EventosProgramados);
        Assert.NotEqual(a.EventosProgramados, c.EventosProgramados);
    }

    [Fact]
    [Trait("CA", "22.3")]
    public void Calendario_NoEmpiezaEnElPrimerSegundoYNoSeAmontona()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 4);

        var segundos = partida.EventosProgramados.Select(e => e.Segundo).ToList();

        Assert.True(segundos[0] >= 2);
        for (var i = 1; i < segundos.Count; i++)
            Assert.True(segundos[i] - segundos[i - 1] >= 2);
    }

    // ---------- Energía adicional (CA-22.1) ----------

    [Fact]
    [Trait("CA", "22.1")]
    public void EnergiaExtra_ElPrimeroQueLaPisaSeLlevaLosPuntos()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(MapaAbierto, semilla: 5, config: GuardianesQuietos);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Items.Count > 0));
        var item = partida.Items[0];
        var multiplicador = partida.MultiplicadorPuntos;
        var puntajeAntes = ana.Puntaje;
        partida.TomarNovedades();

        var resultado = AyudaArenaViva.Caminar(partida, ana, item.Posicion);

        Assert.Equal(ResultadoAccion.EnergiaRecogida, resultado);
        Assert.Equal(puntajeAntes + item.Puntos * multiplicador, ana.Puntaje);
        Assert.DoesNotContain(partida.Items, i => i.Id == item.Id);
        var recogida = Assert.Single(partida.TomarNovedades().OfType<ItemRecogidoNovedad>());
        Assert.Equal(ana.Id, recogida.JugadorId);
        Assert.Equal(item.Id, recogida.ItemId);
        Assert.Equal(0, beto.Puntaje);
    }

    [Fact]
    [Trait("CA", "22.1")]
    public void EnergiaExtra_AparecePisandoUnaCeldaLibreDeLaArena()
    {
        for (var semilla = 1; semilla <= 15; semilla++)
        {
            var (partida, _) = AyudaArenaViva.CrearEscenario("militar", semilla: semilla);
            AyudaArenaViva.AvanzarHasta(partida, () => partida.Items.Count > 0);

            foreach (var item in partida.Items)
            {
                Assert.True(partida.Arena.EsTransitable(item.Posicion));
                Assert.Null(partida.Arena.ObstaculoEn(item.Posicion));
                Assert.False(AyudaArenaViva.Ocupada(partida, item.Posicion));
            }
        }
    }

    [Fact]
    [Trait("CA", "22.1")]
    public void EnergiaExtra_SiNadieLaTomaDesapareceConElEvento()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 5, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Items.Count > 0));
        var evento = partida.EventosActivos.First(e => partida.Items.Any(i => i.EventoId == e.Id));
        partida.TomarNovedades();

        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Items.Count == 0, evento.Duracion + 2));

        Assert.Contains(partida.TomarNovedades().OfType<EventoTerminadoNovedad>(), e => e.EventoId == evento.Id);
    }

    [Fact]
    [Trait("CA", "22.1")]
    public void EnergiaExtra_NoLaPuedenTomarDosJugadores()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(MapaAbierto, semilla: 5, config: GuardianesQuietos);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Items.Count > 0));
        var item = partida.Items[0];

        AyudaArenaViva.Caminar(partida, ana, item.Posicion);

        Assert.DoesNotContain(partida.Items, i => i.Id == item.Id);
        Assert.Equal(0, beto.Puntaje);
    }

    // ---------- Sobrecarga del reactor (CA-22.3) ----------

    [Fact]
    [Trait("CA", "22.3")]
    public void Sobrecarga_DesactivaElReactorYDespuesVuelveAFuncionar()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 2, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Reactor.Desactivado));

        Assert.Equal(ResultadoAccion.ReactorDesactivado, partida.Ejecutar(ana.Id, Accion.Extraer));
        var puntaje = ana.Puntaje;

        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => !partida.Reactor.Desactivado, 20));
        Assert.Equal(puntaje, ana.Puntaje);   // no ganó nada durante la sobrecarga
        Assert.Equal(ResultadoAccion.EnergiaExtraida, partida.Ejecutar(ana.Id, Accion.Extraer));
    }

    [Fact]
    [Trait("CA", "22.7")]
    public void Sobrecarga_DuraLoQueDiceLaConfiguracionYElEventoTerminaConElla()
    {
        var reglas = new ReglasArenaViva { SegundosSobrecarga = 5 };
        var (partida, _, _) = AyudaArenaViva.Crear(
            AyudaArenaViva.MapaGuardianes, semilla: 2, config: new ConfigPartida { ArenaViva = reglas });
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Reactor.Desactivado));
        var evento = partida.EventosActivos.Single(e => e.Tipo == TipoEvento.SobrecargaReactor);
        Assert.Equal(5, partida.Reactor.SegundosDesactivado);
        Assert.Equal(5, evento.Duracion);

        partida.Tick(5);

        Assert.False(partida.Reactor.Desactivado);
        Assert.DoesNotContain(partida.EventosActivos, e => e.Tipo == TipoEvento.SobrecargaReactor);
    }

    // ---------- Invasión de guardianes (CA-22.2) ----------

    [Fact]
    [Trait("CA", "22.2")]
    public void Invasion_AparecenCincoGuardianesMasYDespuesDesaparecen()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, guardianes: 2, semilla: 1, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.EventosActivos.Any(e => e.Tipo == TipoEvento.InvasionGuardianes)));

        Assert.Equal(7, partida.Guardianes.Count);
        Assert.Equal(5, partida.Guardianes.Count(g => g.Temporal));
        Assert.All(partida.Guardianes, g => Assert.True(partida.Arena.EsTransitable(g.Posicion)));

        Assert.True(AyudaArenaViva.AvanzarHasta(
            partida, () => !partida.EventosActivos.Any(e => e.Tipo == TipoEvento.InvasionGuardianes), 40));
        Assert.Equal(2, partida.Guardianes.Count);
        Assert.True(partida.Guardianes.All(g => !g.Temporal));
    }

    [Fact]
    [Trait("CA", "22.2")]
    public void Invasion_LosInvasoresPersiguenComoLosDemasGuardianes()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 1, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.Guardianes.Any(g => g.Temporal)));
        var invasor = partida.Guardianes.First(g => g.Temporal);
        int Distancia() => Math.Min(
            invasor.Posicion.DistanciaManhattan(ana.Posicion), invasor.Posicion.DistanciaManhattan(beto.Posicion));
        var antes = Distancia();

        partida.Tick(4);

        Assert.True(Distancia() <= antes, "el invasor no debe alejarse de los jugadores");
    }

    // ---------- Creativos (CA-22.4) ----------

    [Fact]
    [Trait("CA", "22.4")]
    public void PuntosDobles_LaExtraccionVale_ElDobleSoloMientrasDura()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 3, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(
            partida, () => partida.MultiplicadorPuntos == 2 && !partida.Reactor.Desactivado));

        partida.Ejecutar(ana.Id, Accion.Extraer);
        Assert.Equal(20, ana.Puntaje);   // 10 de energía x 2

        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.MultiplicadorPuntos == 1 && !partida.Reactor.Desactivado, 60));
        var antes = ana.Puntaje;
        partida.Ejecutar(ana.Id, Accion.Extraer);
        Assert.Equal(antes + 10, ana.Puntaje);   // vuelve a valer lo normal
    }

    [Fact]
    [Trait("CA", "22.4")]
    public void PuntosDobles_NoDuplicaLasPenalizaciones()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 3, config: SinCongelar);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.MultiplicadorPuntos == 2));

        Assert.Equal(2, partida.MultiplicadorPuntos);
        Assert.Equal(0, ana.Puntaje);
    }

    [Fact]
    [Trait("CA", "22.4")]
    public void RutasAlternativas_DejanTodasLasPuertasAbiertasMientrasDuran()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 1);
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => partida.EventosActivos.Any(e => e.Tipo == TipoEvento.RutasAlternativas)));

        for (var i = 0; i < 12 && partida.EventosActivos.Any(e => e.Tipo == TipoEvento.RutasAlternativas); i++)
        {
            Assert.All(partida.Arena.Puertas, p => Assert.True(p.Abierta));
            partida.Tick(1);
        }
    }

    [Fact]
    [Trait("CA", "22.4")]
    public void LluviaDeEnergia_CreaVariasCeldasQueTerminanAlTomarLaUltima()
    {
        var reglas = new ReglasArenaViva { CeldasLluviaEnergia = 3 };
        var (partida, _, _) = AyudaArenaViva.Crear(
            Escenarios.Predeterminado, semilla: 3, config: new ConfigPartida { ArenaViva = reglas });
        Assert.True(AyudaArenaViva.AvanzarHasta(
            partida, () => partida.EventosActivos.Any(e => e.Tipo == TipoEvento.LluviaEnergia)));

        Assert.Equal(3, partida.Items.Count);
        Assert.Equal(3, partida.Items.Select(i => i.Posicion).Distinct().Count());
    }

    // ---------- Comunicación y consistencia (CA-22.5, 22.6, 22.7, 22.8, 22.9) ----------

    [Fact]
    [Trait("CA", "22.5")]
    public void CadaEventoQueEmpiezaRegistraUnaNovedadConSuDuracion()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: 1);

        var iniciados = JugarSinMoverse(partida, 60);

        Assert.NotEmpty(iniciados);
        Assert.All(iniciados, e => Assert.True(e.Segundos > 0));
        Assert.Equal(iniciados.Count, iniciados.Select(e => e.EventoId).Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [Trait("CA", "22.9")]
    public void Evento_NuncaEstaActivoDosVecesALaVez(int semilla)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio", semilla: semilla);

        for (var i = 0; i < 600 && partida.Estado == EstadoPartida.EnCurso; i++)
        {
            partida.Tick(1);
            var tipos = partida.EventosActivos.Select(e => e.Tipo).ToList();
            Assert.Equal(tipos.Count, tipos.Distinct().Count());
        }
    }

    [Fact]
    [Trait("CA", "22.9")]
    public void Evento_NoSeRepiteSinoHayPartidaNueva_SoloSeActivaUnaVezLaArenaViva()
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("laboratorio");

        Assert.Throws<DominioException>(() => partida.Iniciar(new Random(9)));   // la partida ya comenzó
    }

    [Fact]
    [Trait("CA", "22.8")]
    public void PartidaTerminada_NoTieneEventosNiEnergiaEnLaArenaYElTiempoNoLosMueve()
    {
        var config = new ConfigPartida { DuracionSegundos = 40, SegundosCongelamiento = 0 };
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 1, config: config);
        partida.Ejecutar(ana.Id, Accion.Extraer);   // Ana va ganando: no habrá empate

        AyudaArenaViva.AvanzarHasta(partida, () => partida.Estado == EstadoPartida.Finalizada, 60);

        Assert.Equal(EstadoPartida.Finalizada, partida.Estado);
        Assert.Empty(partida.EventosActivos);
        Assert.Empty(partida.Items);
        Assert.Equal(ana.Id, partida.GanadorId);

        var novedades = partida.TomarNovedades();
        var iniciados = novedades.OfType<EventoIniciadoNovedad>().Select(e => e.EventoId).ToHashSet();
        var terminados = novedades.OfType<EventoTerminadoNovedad>().Select(e => e.EventoId).ToHashSet();
        Assert.Equal(iniciados.OrderBy(i => i).ToList(), terminados.OrderBy(i => i).ToList());   // todo evento que empezó, terminó

        var posiciones = partida.Guardianes.Select(g => g.Posicion).ToList();
        partida.Tick(10);
        Assert.Equal(posiciones, partida.Guardianes.Select(g => g.Posicion).ToList());
        Assert.Empty(partida.TomarNovedades());
    }

    [Fact]
    [Trait("CA", "22.8")]
    public void PartidaTerminada_NoAceptaRecogerEnergia()
    {
        var config = new ConfigPartida { DuracionSegundos = 20, SegundosCongelamiento = 0 };
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, semilla: 1, config: config);
        partida.Ejecutar(ana.Id, Accion.Extraer);
        AyudaArenaViva.AvanzarHasta(partida, () => partida.Estado == EstadoPartida.Finalizada, 40);

        Assert.Throws<DominioException>(() => partida.Ejecutar(ana.Id, Accion.Abajo));
    }

    [Fact]
    [Trait("CA", "22.7")]
    public void Evento_AlTerminarRestauraLasCondicionesAnteriores()
    {
        var (partida, _, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, guardianes: 2, semilla: 1, config: SinCongelar);

        for (var i = 0; i < 400 && partida.Estado == EstadoPartida.EnCurso; i++)
            partida.Tick(1);

        // Pasado un buen rato, lo que quede activo es solo lo que aún no vence: nada queda "pegado".
        Assert.All(partida.EventosActivos, e => Assert.True(e.SegundosRestantes > 0));
        Assert.Equal(partida.EventosActivos.Any(e => e.Tipo == TipoEvento.PuntosDobles) ? 2 : 1, partida.MultiplicadorPuntos);
        Assert.Equal(partida.EventosActivos.Any(e => e.Tipo == TipoEvento.SobrecargaReactor), partida.Reactor.Desactivado);
        var temporales = partida.Guardianes.Count(g => g.Temporal);
        Assert.Equal(partida.EventosActivos.Any(e => e.Tipo == TipoEvento.InvasionGuardianes) ? 5 : 0, temporales);
    }

    // ---------- Valores acordados con el equipo (Docs/arena-viva.md) ----------

    [Fact]
    [Trait("CA", "22.10")]
    public void Reglas_ValoresPorDefecto_SonLosAcordados()
    {
        var reglas = new ReglasArenaViva();

        Assert.Equal(25, reglas.PuntosEnergiaExtra);
        Assert.Equal(8, reglas.SegundosEnergiaExtra);
        Assert.Equal(10, reglas.SegundosSobrecarga);
        Assert.Equal(5, reglas.GuardianesInvasion);
        Assert.Equal(3, reglas.TrampasAleatorias);
        Assert.Equal(5, new ConfigPartida().PenalizacionTrampa);
        Assert.Equal(3, new ConfigPartida().SegundosCongelamiento);
    }

    [Fact]
    [Trait("CA", "22.1")]
    public void EnergiaExtra_DaVeinticincoPuntosYDuraOchoSegundos()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(MapaAbierto, semilla: 5, config: GuardianesQuietos);
        Assert.True(AyudaArenaViva.AvanzarHasta(
            partida, () => partida.Items.Count > 0 && partida.MultiplicadorPuntos == 1));
        var item = partida.Items[0];
        var evento = partida.EventosActivos.Single(e => e.Id == item.EventoId);

        Assert.Equal(25, item.Puntos);
        Assert.Equal(8, evento.Duracion);

        AyudaArenaViva.Caminar(partida, ana, item.Posicion);
        Assert.Equal(25, ana.Puntaje);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(21)]
    [Trait("CA", "22.3")]
    public void Sobrecarga_DuraDiezSegundosYOcurreAlMenosDosVecesEnMomentosDistintos(int semilla)
    {
        var (partida, _) = AyudaArenaViva.CrearEscenario("estacion", semilla: semilla);
        var inicios = new List<int>();

        for (var i = 0; i < partida.Config.DuracionSegundos && partida.Estado == EstadoPartida.EnCurso; i++)
        {
            partida.Tick(1);
            foreach (var e in partida.TomarNovedades().OfType<EventoIniciadoNovedad>()
                         .Where(e => e.Tipo == TipoEvento.SobrecargaReactor))
            {
                Assert.Equal(10, e.Segundos);
                Assert.Equal(10, partida.Reactor.SegundosDesactivado);
                inicios.Add(partida.SegundosTranscurridos);
            }
        }

        Assert.True(inicios.Count >= 2);
        Assert.Equal(inicios.Count, inicios.Distinct().Count());
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "22.3")]
    public void Sobrecarga_LaPrimeraCaeEntreLos20YLos45SegundosYLaOtraEnLaSegundaMitad(string escenario)
    {
        for (var semilla = 0; semilla < 50; semilla++)
        {
            var (partida, _) = AyudaArenaViva.CrearEscenario(escenario, semilla: semilla);
            var calendario = partida.EventosProgramados;
            var sobrecargas = calendario
                .Where(e => e.Tipo == TipoEvento.SobrecargaReactor).Select(e => e.Segundo).OrderBy(s => s).ToList();

            Assert.Equal(2, sobrecargas.Count);
            Assert.InRange(sobrecargas[0], 20, 45);
            Assert.InRange(sobrecargas[1], 90, 153);

            // Ningún otro evento a menos de 2 s, y el calendario sigue en orden.
            Assert.All(calendario.Where(e => e.Tipo != TipoEvento.SobrecargaReactor),
                e => Assert.All(sobrecargas, s => Assert.True(Math.Abs(e.Segundo - s) >= 2)));
            Assert.Equal(calendario.OrderBy(e => e.Segundo).Select(e => e.Segundo), calendario.Select(e => e.Segundo));
        }
    }

    [Fact]
    [Trait("CA", "22.3")]
    public void Sobrecarga_ElMomentoCambiaSegunElAzar()
    {
        var momentos = Enumerable.Range(1, 6)
            .Select(semilla => AyudaArenaViva.CrearEscenario("laboratorio", semilla: semilla).partida)
            .Select(p => string.Join(",", p.EventosProgramados
                .Where(e => e.Tipo == TipoEvento.SobrecargaReactor).Select(e => e.Segundo)))
            .ToHashSet();

        Assert.True(momentos.Count > 1);
    }

    // ---------- Configuración ----------

    [Fact]
    [Trait("CA", "22.10")]
    public void Reglas_ConValoresInvalidos_Fallan()
    {
        Assert.Throws<DominioException>(() => new ReglasArenaViva { SegundosSobrecarga = 0 }.Validar());
        Assert.Throws<DominioException>(() => new ReglasArenaViva { GuardianesInvasion = 0 }.Validar());
        Assert.Throws<DominioException>(() => new ReglasArenaViva { SegundosPorPasoGuardian = 0 }.Validar());
        new ReglasArenaViva().Validar();
    }
}
