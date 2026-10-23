using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Dominio.Historial;
using Dominio.Juego;
using Dominio.Usuarios;
using Infraestructura.Partidas;
using Infraestructura.Persistencia;
using Xunit;

namespace PruebasAplicacion;

/// <summary>US-04: historial, estadísticas, logros y ranking, con persistencia REAL en JSON temporal.</summary>
public sealed class Pruebas_PerfilHistorial : IDisposable
{
    private static readonly Guid Ana = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beto = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "reactor-historial-" + Guid.NewGuid().ToString("N"));
    private readonly RelojFijo reloj = new();

    public void Dispose()
    {
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
    }

    private JsonHistorialRepositorio NuevoRepo() =>
        new(new JsonFileStore<PartidaHistorialRegistro>(Path.Combine(carpeta, "historial-partidas.json")));

    private static RegistroPartida Partida(Guid? ganador, int puntosAna, int puntosBeto, DateTime? fin = null) =>
        new(Guid.NewGuid(), fin ?? DateTime.UtcNow, 60, "Tiempo", 0, ganador,
        [
            new JugadorRegistro(Ana, "Ana", null, puntosAna, ganador == Ana),
            new JugadorRegistro(Beto, "Beto", null, puntosBeto, ganador == Beto),
        ]);

    // ---------- Estadísticas y logros (dominio puro) ----------

    [Fact]
    public void Estadisticas_SinPartidas_EstanEnCero()
    {
        var e = Estadisticas.Calcular(Ana, []);

        Assert.Equal(Estadisticas.Vacia, e);
        Assert.Equal(0, e.Derrotas);
        Assert.Equal(0, e.PromedioPuntos);
    }

    [Fact]
    public void Estadisticas_SumanPartidasVictoriasYPuntos()
    {
        var partidas = new[] { Partida(Ana, 30, 10), Partida(Beto, 5, 20), Partida(Ana, 40, 1) };

        var e = Estadisticas.Calcular(Ana, partidas);

        Assert.Equal(3, e.PartidasJugadas);
        Assert.Equal(2, e.Victorias);
        Assert.Equal(1, e.Derrotas);
        Assert.Equal(75, e.PuntosTotales);
        Assert.Equal(40, e.MejorPuntaje);
        Assert.Equal(25, e.PromedioPuntos);
    }

    [Fact]
    public void Estadisticas_IgnoranPartidasDondeNoParticipo()
    {
        var ajena = new RegistroPartida(Guid.NewGuid(), DateTime.UtcNow, 60, "Tiempo", 0, Beto,
            [new JugadorRegistro(Beto, "Beto", null, 10, true)]);

        Assert.Equal(0, Estadisticas.Calcular(Ana, [ajena]).PartidasJugadas);
    }

    [Fact]
    public void Logros_SinPartidas_NingunoObtenido()
    {
        Assert.All(Logro.Evaluar(Estadisticas.Vacia), l => Assert.False(l.Obtenido));
        Assert.Equal(4, Logro.Evaluar(Estadisticas.Vacia).Count);
    }

    [Fact]
    public void Logros_SeObtienenAlCumplirLaMeta()
    {
        var logros = Logro.Evaluar(new Estadisticas(PartidasJugadas: 6, Victorias: 5, PuntosTotales: 100, MejorPuntaje: 30))
            .ToDictionary(l => l.Id);

        Assert.True(logros["primera-partida"].Obtenido);
        Assert.True(logros["primera-victoria"].Obtenido);
        Assert.True(logros["cinco-victorias"].Obtenido);
        Assert.True(logros["cien-puntos"].Obtenido);
    }

    [Fact]
    public void Logros_ConAvanceParcial_NoSeObtienenYMuestranElProgreso()
    {
        var logros = Logro.Evaluar(new Estadisticas(PartidasJugadas: 2, Victorias: 2, PuntosTotales: 60, MejorPuntaje: 30))
            .ToDictionary(l => l.Id);

        Assert.True(logros["primera-victoria"].Obtenido);
        Assert.False(logros["cinco-victorias"].Obtenido);
        Assert.Equal(2, logros["cinco-victorias"].Progreso);
        Assert.False(logros["cien-puntos"].Obtenido);
        Assert.Equal(60, logros["cien-puntos"].Progreso);
    }

    // ---------- Ranking (dominio puro) ----------

    [Fact]
    public void Ranking_PorVictorias_GanaQuienMasPartidasGano()
    {
        var partidas = new[] { Partida(Beto, 5, 20), Partida(Beto, 1, 9), Partida(Ana, 50, 2) };

        var ranking = Ranking.Construir(partidas, CriterioRanking.Victorias);

        Assert.Equal(new[] { "Beto", "Ana" }, ranking.Select(x => x.Nombre));
        Assert.Equal(new[] { 1, 2 }, ranking.Select(x => x.Posicion));
        Assert.Equal(2, ranking[0].Victorias);
    }

    [Fact]
    public void Ranking_PorPuntos_GanaQuienMasPuntosSumo()
    {
        var partidas = new[] { Partida(Beto, 5, 20), Partida(Beto, 1, 9), Partida(Ana, 50, 2) };

        var ranking = Ranking.Construir(partidas, CriterioRanking.Puntos);

        Assert.Equal(new[] { "Ana", "Beto" }, ranking.Select(x => x.Nombre));   // Ana 56, Beto 31
        Assert.Equal(56, ranking[0].PuntosTotales);
    }

    [Fact]
    public void Ranking_EmpateEnVictorias_DesempataPorPuntos()
    {
        var partidas = new[] { Partida(Ana, 10, 5), Partida(Beto, 3, 40) };

        var ranking = Ranking.Construir(partidas, CriterioRanking.Victorias);

        Assert.Equal("Beto", ranking[0].Nombre);   // 1 victoria cada uno; Beto sumó 45 contra 13
    }

    [Fact]
    public void Ranking_RespetaElLimite_YSinPartidasEstaVacio()
    {
        var partidas = new[] { Partida(Ana, 10, 5) };

        Assert.Single(Ranking.Construir(partidas, CriterioRanking.Puntos, limite: 1));
        Assert.Empty(Ranking.Construir([], CriterioRanking.Puntos));
    }

    // ---------- Historial en JSON ----------

    [Fact]
    public async Task Historial_GuardaYSobreviveAlReinicio()
    {
        var partida = Partida(Ana, 10, 5);
        Assert.True(await NuevoRepo().RegistrarAsync(partida));

        var tras = await NuevoRepo().ListarTodasAsync();   // "reiniciar" = reabrir el archivo

        Assert.Single(tras);
        Assert.Equal(partida.PartidaId, tras[0].PartidaId);
        Assert.Equal(Ana, tras[0].GanadorId);
        Assert.Equal(2, tras[0].Jugadores.Count);
    }

    [Fact]
    public async Task Historial_RegistrarLaMismaPartidaDosVeces_NoDuplica()
    {
        var repo = NuevoRepo();
        var partida = Partida(Ana, 10, 5);

        Assert.True(await repo.RegistrarAsync(partida));
        Assert.False(await repo.RegistrarAsync(partida));
        Assert.Single(await repo.ListarTodasAsync());
    }

    [Fact]
    public async Task Historial_DeUnJugador_VieneDeLaMasRecienteAlaMasAntigua()
    {
        var repo = NuevoRepo();
        var vieja = Partida(Ana, 1, 1, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var nueva = Partida(Beto, 1, 1, new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        await repo.RegistrarAsync(vieja);
        await repo.RegistrarAsync(nueva);

        var lista = await repo.ListarDeJugadorAsync(Ana);

        Assert.Equal(new[] { nueva.PartidaId, vieja.PartidaId }, lista.Select(p => p.PartidaId));
    }

    // ---------- Caso de uso: registrar el resultado ----------

    private static PartidaVista Vista(EstadoPartida estado, Guid? ganador) => new(
        Guid.NewGuid(), estado, 0, ganador, 4, 15, 11, 7, 5, 0, 0, 0,
        [
            new JugadorVista(Ana, "Ana", Guid.NewGuid(), 1, 1, 3, 0, false, 12),
            new JugadorVista(Beto, "Beto", Guid.NewGuid(), 13, 1, 3, 0, false, 7),
        ],
        [], [], Ana, ModoFin.Tiempo, 20, 60, 1);

    [Fact]
    public async Task Registrar_PartidaFinalizada_GuardaGanadorPuntajesYProrrogas()
    {
        var repo = NuevoRepo();
        var caso = new RegistrarResultadoPartida(repo, reloj);
        var vista = Vista(EstadoPartida.Finalizada, Ana);

        Assert.True(await caso.RegistrarAsync(vista));

        var guardada = Assert.Single(await repo.ListarTodasAsync());
        Assert.Equal(Ana, guardada.GanadorId);
        Assert.Equal(1, guardada.Prorrogas);
        Assert.Equal(60, guardada.DuracionSegundos);
        Assert.Equal("Tiempo", guardada.ModoFin);
        Assert.Equal(reloj.UtcNow, guardada.Fin);
        Assert.True(guardada.Jugadores.Single(j => j.UsuarioId == Ana).EsGanador);
        Assert.False(guardada.Jugadores.Single(j => j.UsuarioId == Beto).EsGanador);
        Assert.Equal(7, guardada.Jugadores.Single(j => j.UsuarioId == Beto).Puntaje);
    }

    [Theory]
    [InlineData(EstadoPartida.Espera)]
    [InlineData(EstadoPartida.EnCurso)]
    public async Task Registrar_PartidaQueNoTermino_NoGuardaNada(EstadoPartida estado)
    {
        var repo = NuevoRepo();

        Assert.False(await new RegistrarResultadoPartida(repo, reloj).RegistrarAsync(Vista(estado, null)));
        Assert.Empty(await repo.ListarTodasAsync());
    }

    [Fact]
    public async Task Registrar_DosVecesLaMismaPartida_GuardaUnaSola()
    {
        var repo = NuevoRepo();
        var caso = new RegistrarResultadoPartida(repo, reloj);
        var vista = Vista(EstadoPartida.Finalizada, Ana);

        Assert.True(await caso.RegistrarAsync(vista));
        Assert.False(await caso.RegistrarAsync(vista));
        Assert.Single(await repo.ListarTodasAsync());
    }

    // ---------- Decorador: guarda sin tocar el reloj ni los botones ----------

    [Fact]
    public async Task Decorador_AlTerminarPorTiempo_GuardaLaPartidaEnElHistorial()
    {
        var historial = NuevoRepo();
        var repo = new PartidaRepoConHistorial(
            new PartidaEnMemoriaRepositorio(), new RegistrarResultadoPartida(historial, reloj));
        var partida = new Partida(Guid.NewGuid(), new ConfigPartida { DuracionSegundos = 5 }, Arena.DesdeFilas(Escenarios.Predeterminado));
        partida.Unirse(Ana, "Ana", Guid.NewGuid());
        partida.Unirse(Beto, "Beto", Guid.NewGuid());
        partida.Iniciar();
        await repo.AgregarAsync(partida);

        await repo.UsarAsync(partida.Id, p => p.Ejecutar(Ana, Accion.Abajo));   // Ana suma 1 punto
        Assert.Empty(await historial.ListarTodasAsync());                          // aún en curso

        await repo.UsarAsync(partida.Id, p => { p.Tick(5); return 0; });           // se acaba el tiempo

        var guardada = Assert.Single(await historial.ListarTodasAsync());
        Assert.Equal(partida.Id, guardada.PartidaId);
        Assert.Equal(Ana, guardada.GanadorId);
    }

    [Fact]
    public async Task Decorador_ConsultarUnaPartidaTerminadaVariasVeces_NoDuplica()
    {
        var historial = NuevoRepo();
        var repo = new PartidaRepoConHistorial(
            new PartidaEnMemoriaRepositorio(), new RegistrarResultadoPartida(historial, reloj));
        var partida = new Partida(Guid.NewGuid(), new ConfigPartida { DuracionSegundos = 5 }, Arena.DesdeFilas(Escenarios.Predeterminado));
        partida.Unirse(Ana, "Ana", Guid.NewGuid());
        partida.Unirse(Beto, "Beto", Guid.NewGuid());
        partida.Iniciar();
        await repo.AgregarAsync(partida);
        await repo.UsarAsync(partida.Id, p => p.Ejecutar(Ana, Accion.Abajo));
        await repo.UsarAsync(partida.Id, p => { p.Tick(5); return 0; });

        for (var i = 0; i < 3; i++)
            await repo.UsarAsync(partida.Id, PartidaVista.Desde);

        Assert.Single(await historial.ListarTodasAsync());
    }

    [Fact]
    public async Task Decorador_SiFallaElGuardado_LaPartidaSigueFuncionando()
    {
        var fallos = new List<Guid>();
        var repo = new PartidaRepoConHistorial(
            new PartidaEnMemoriaRepositorio(),
            new RegistrarResultadoPartida(new HistorialQueFalla(), reloj),
            (id, _) => fallos.Add(id));
        var partida = new Partida(Guid.NewGuid(), new ConfigPartida { DuracionSegundos = 5 }, Arena.DesdeFilas(Escenarios.Predeterminado));
        partida.Unirse(Ana, "Ana", Guid.NewGuid());
        partida.Unirse(Beto, "Beto", Guid.NewGuid());
        partida.Iniciar();
        await repo.AgregarAsync(partida);
        await repo.UsarAsync(partida.Id, p => p.Ejecutar(Ana, Accion.Abajo));

        var estado = await repo.UsarAsync(partida.Id, p => { p.Tick(5); return p.Estado; });

        Assert.Equal(EstadoPartida.Finalizada, estado);   // el jugador no se entera del fallo
        Assert.Equal(new[] { partida.Id }, fallos);
    }

    // ---------- Perfil, historial y ranking (servicio) ----------

    private sealed class UsuariosEnMemoria : IUsuarioRepo
    {
        private readonly List<Usuario> lista = [];
        public void Agregar(Usuario u) => lista.Add(u);
        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(lista.FirstOrDefault(u => u.Id == id));
        public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default) => Task.FromResult(lista.FirstOrDefault(u => u.Nombre == username));
        public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default) => Task.FromResult(lista.FirstOrDefault(u => u.Correo == correo));
        public Task GuardarAsync(Usuario usuario, CancellationToken ct = default) { lista.Add(usuario); return Task.CompletedTask; }
    }

    private async Task<PerfilJugadorService> ServicioConPartidasAsync()
    {
        var usuarios = new UsuariosEnMemoria();
        var ana = new Usuario(Ana, "Ana", "ana@correo.com", "hash");
        ana.MarcarVerificado();
        usuarios.Agregar(ana);

        var repo = NuevoRepo();
        await repo.RegistrarAsync(Partida(Ana, 30, 10, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
        await repo.RegistrarAsync(Partida(Beto, 5, 20, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)));
        return new PerfilJugadorService(usuarios, repo);
    }

    [Fact]
    public async Task Perfil_DevuelveDatosEstadisticasYLogros()
    {
        var perfil = await (await ServicioConPartidasAsync()).ObtenerPerfilAsync(Ana);

        Assert.Equal("Ana", perfil.Username);
        Assert.True(perfil.EmailVerified);
        Assert.Equal(2, perfil.Estadisticas.PartidasJugadas);
        Assert.Equal(1, perfil.Estadisticas.Victorias);
        Assert.Equal(35, perfil.Estadisticas.PuntosTotales);
        Assert.True(perfil.Logros.Single(l => l.Id == "primera-victoria").Obtenido);
        Assert.False(perfil.Logros.Single(l => l.Id == "cinco-victorias").Obtenido);
    }

    [Fact]
    public async Task Perfil_DeUnUsuarioInexistente_Falla()
    {
        var servicio = await ServicioConPartidasAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.ObtenerPerfilAsync(Guid.NewGuid()));

        Assert.Equal("USUARIO_NO_ENCONTRADO", error.Codigo);
    }

    [Fact]
    public async Task Historial_MuestraElResultadoDesdeElPuntoDeVistaDelJugador()
    {
        var servicio = await ServicioConPartidasAsync();

        var historial = await servicio.ObtenerHistorialAsync(Ana);

        Assert.Equal(new[] { "Derrota", "Victoria" }, historial.Select(h => h.Resultado));   // la más reciente primero
        Assert.Equal(new[] { 5, 30 }, historial.Select(h => h.MiPuntaje));
        Assert.All(historial, h => Assert.Equal(2, h.Jugadores.Count));
    }

    [Fact]
    public async Task Historial_RespetaElLimite()
    {
        var servicio = await ServicioConPartidasAsync();

        Assert.Single(await servicio.ObtenerHistorialAsync(Ana, limite: 1));
    }

    [Fact]
    public async Task Ranking_ConCriterioValido_DevuelveLaLista()
    {
        var servicio = await ServicioConPartidasAsync();

        var porPuntos = await servicio.ObtenerRankingAsync("PUNTOS");   // sin distinguir mayúsculas
        var porDefecto = await servicio.ObtenerRankingAsync(null);

        Assert.Equal(new[] { "Ana", "Beto" }, porPuntos.Select(x => x.Username));   // Ana 35, Beto 30
        Assert.Equal(2, porDefecto.Count);
    }

    [Fact]
    public async Task Ranking_ConCriterioInvalido_Falla()
    {
        var servicio = await ServicioConPartidasAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.ObtenerRankingAsync("edad"));

        Assert.Equal("CRITERIO_INVALIDO", error.Codigo);
    }

    private sealed class RelojFijo : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class HistorialQueFalla : IHistorialRepo
    {
        public Task<bool> RegistrarAsync(RegistroPartida partida, CancellationToken ct = default) => throw new IOException("disco lleno");
        public Task<IReadOnlyList<RegistroPartida>> ListarTodasAsync(CancellationToken ct = default) => throw new IOException();
        public Task<IReadOnlyList<RegistroPartida>> ListarDeJugadorAsync(Guid usuarioId, CancellationToken ct = default) => throw new IOException();
    }
}
