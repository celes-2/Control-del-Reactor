using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using API.TiempoReal;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Contratos.TiempoReal;
using Dominio.Comun;
using Dominio.Juego;
using Dominio.Usuarios;
using Infraestructura.Partidas;
using Xunit;

namespace PruebasAPI;

/// <summary>
/// Sprint 3, Paquete B en tiempo real: mensajes arena:update, event:started, event:ended, player:frozen
/// e item:collected, y el campo live de match:state. Ver contratos-websocket.md y Docs/arena-viva.md.
/// </summary>
public class ArenaVivaTiempoRealTests
{
    // ---------- utilidades ----------

    /// <summary>WebSocket falso: guarda lo que el servidor le envía.</summary>
    private sealed class SocketFalso : WebSocket
    {
        public List<string> Enviados { get; } = [];

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override void Dispose() { }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct) =>
            throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType tipo, bool fin, CancellationToken ct)
        {
            Enviados.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }

        public List<string> Tipos() =>
            Enviados.Select(t => JsonDocument.Parse(t).RootElement.GetProperty("type").GetString()!).ToList();
    }

    private static (Partida partida, Jugador ana, Jugador beto) PartidaViva(
        string? escenario = "laboratorio", int semilla = 1, ConfigPartida? config = null)
    {
        var definicion = Escenarios.Obtener(escenario);
        var partida = Partida.Crear(
            (config ?? new ConfigPartida()) with { EscenarioId = escenario },
            Arena.DesdeFilas(definicion.Filas));
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar(new Random(semilla));
        return (partida, ana, beto);
    }

    private static (PublicadorPartida publicador, SocketFalso socket, Guid partidaId) PublicadorConUnJugador()
    {
        var salas = new AdministradorSalas();
        var socket = new SocketFalso();
        var partidaId = Guid.NewGuid();
        salas.Agregar(partidaId, new ConexionJugador(Guid.NewGuid(), socket));
        return (new PublicadorPartida(salas, new PartidaEnMemoriaRepositorio()), socket, partidaId);
    }

    // ---------- Reloj (CA-20.7, CA-22.5) ----------

    [Fact]
    [Trait("CA", "20.7")]
    public void Reloj_ConArenaViva_TraeLaFotoDeLaArenaCadaSegundo()
    {
        var (partida, _, _) = PartidaViva();

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        var arena = Assert.IsType<ArenaActualizadaData>(cambio.Arena);
        Assert.Equal(1, arena.TimeElapsed);
        Assert.Equal(4, arena.Live.Guardians.Count);
        Assert.True(arena.Live.Traps.Count >= 3);
        Assert.NotEmpty(arena.Live.Doors);
        Assert.Equal(1, arena.Live.ScoreMultiplier);
    }

    [Fact]
    public void Reloj_SinArenaViva_NoTraeArenaNiNovedades()
    {
        var partida = Partida.Crear(new ConfigPartida());
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar();

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        Assert.Null(cambio.Arena);
        Assert.NotNull(cambio.Novedades);
        Assert.Empty(cambio.Novedades);
    }

    [Fact]
    [Trait("CA", "22.5")]
    public void Reloj_CuandoEmpiezaUnEvento_LoTraeEnLasNovedadesUnaSolaVez()
    {
        var (partida, _, _) = PartidaViva();
        var iniciados = new List<EventoIniciadoNovedad>();

        for (var i = 0; i < 60; i++)
            iniciados.AddRange(ServicioReloj.Avanzar(partida)!.Novedades!.OfType<EventoIniciadoNovedad>());

        Assert.NotEmpty(iniciados);
        Assert.Equal(iniciados.Count, iniciados.Select(e => e.EventoId).Distinct().Count());
    }

    [Fact]
    [Trait("CA", "21.7")]
    public void Reloj_CuandoUnGuardianAlcanzaAUnJugador_LoTraeEnLasNovedades()
    {
        var (partida, _, _) = PartidaViva(escenario: null);   // mapa clásico, guardianes en los bordes
        var congelados = new List<JugadorCongeladoNovedad>();

        for (var i = 0; i < 40 && congelados.Count == 0; i++)
            congelados.AddRange(ServicioReloj.Avanzar(partida)!.Novedades!.OfType<JugadorCongeladoNovedad>());

        var primero = Assert.Single(congelados.Take(1));
        Assert.Equal(OrigenCongelamiento.Guardian, primero.Origen);
        Assert.Equal(3, primero.Segundos);
    }

    // ---------- Mapeo a mensajes ----------

    [Fact]
    [Trait("CA", "21.7")]
    public void AMensaje_Congelamiento_EsPlayerFrozenConOrigenYTrampa()
    {
        var id = Guid.NewGuid();

        var porTrampa = new JugadorCongeladoNovedad(id, 5, OrigenCongelamiento.Trampa, TipoTrampa.CeldaEmergente).AMensaje();
        var porGuardian = new JugadorCongeladoNovedad(id, 3, OrigenCongelamiento.Guardian, null).AMensaje();

        Assert.Equal(TiposMensaje.PlayerFrozen, porTrampa!.Value.Tipo);
        Assert.Equal(new JugadorCongeladoData(id, 5, "TRAP", "RISING_CELL"), porTrampa.Value.Data);
        Assert.Equal(new JugadorCongeladoData(id, 3, "GUARDIAN", null), porGuardian!.Value.Data);
    }

    [Fact]
    [Trait("CA", "22.5")]
    public void AMensaje_Eventos_LlevanTipoDuracionYUnTextoParaMostrar()
    {
        var evento = Guid.NewGuid();

        var inicio = new EventoIniciadoNovedad(evento, TipoEvento.SobrecargaReactor, 10).AMensaje();
        var fin = new EventoTerminadoNovedad(evento, TipoEvento.SobrecargaReactor).AMensaje();

        Assert.Equal(TiposMensaje.EventStarted, inicio!.Value.Tipo);
        var data = Assert.IsType<EventoIniciadoData>(inicio.Value.Data);
        Assert.Equal("REACTOR_OVERLOAD", data.Kind);
        Assert.Equal(10, data.Duration);
        Assert.Contains("10 s", data.Message);
        Assert.Equal(TiposMensaje.EventEnded, fin!.Value.Tipo);
        Assert.Equal(new EventoTerminadoData(evento, "REACTOR_OVERLOAD"), fin.Value.Data);
    }

    [Fact]
    [Trait("CA", "22.1")]
    public void AMensaje_EnergiaRecogida_EsItemCollected()
    {
        var jugador = Guid.NewGuid();

        var mensaje = new ItemRecogidoNovedad(3, jugador, 25).AMensaje();

        Assert.Equal(TiposMensaje.ItemCollected, mensaje!.Value.Tipo);
        Assert.Equal(new ItemRecogidoData(3, jugador, 25), mensaje.Value.Data);
    }

    [Fact]
    public void Textos_CadaTrampaYCadaEventoTieneSuCodigoPropio()
    {
        var trampas = Enum.GetValues<TipoTrampa>().Select(t => t.ATexto()).ToList();
        var eventos = Enum.GetValues<TipoEvento>().Select(e => e.ATexto()).ToList();

        Assert.Equal(new[] { "QUICKSAND", "RISING_CELL", "NET" }, trampas.ToArray());
        Assert.Equal(
            new[] { "EXTRA_ENERGY", "GUARDIAN_INVASION", "REACTOR_OVERLOAD", "DOUBLE_POINTS", "ALTERNATE_ROUTES", "ENERGY_RAIN" },
            eventos.ToArray());
        Assert.All(Enum.GetValues<TipoEvento>(), e => Assert.DoesNotContain("Evento especial", MapeoTiempoReal.MensajeDeEvento(e, 5)));
    }

    [Fact]
    [Trait("CA", "23.4")]
    [Trait("CA", "21.4")]
    [Trait("CA", "20.6")]
    public void EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados()
    {
        var (partida, ana, _) = PartidaViva(escenario: "militar");
        partida.Congelar(ana.Id);

        var estado = PartidaVista.Desde(partida).AEstadoPartida(partida.Config.DuracionSegundos);

        Assert.Equal("militar", estado.ScenarioId);
        Assert.Equal(4, estado.GuardianCount);
        var live = Assert.IsType<ArenaVivaData>(estado.Live);
        Assert.Equal(4, live.Guardians.Count);
        Assert.Equal(3, estado.Players.Single(p => p.UserId == ana.Id).FrozenSeconds);
        Assert.Equal(new CongeladoData(ana.Id, 3), Assert.Single(live.Frozen));
        Assert.All(estado.Scenario.Rows, f => Assert.Matches("^[W.SRT]+$", f));   // D y G no viajan en rows
    }

    [Fact]
    public void EstadoPartida_EnEspera_NoTieneLive()
    {
        var partida = Partida.Crear(new ConfigPartida(), Arena.DesdeFilas(Escenarios.Obtener("laboratorio").Filas));

        var estado = PartidaVista.Desde(partida).AEstadoPartida(partida.Config.DuracionSegundos);

        Assert.Null(estado.Live);
    }

    [Fact]
    public void Serializar_ArenaUpdate_UsaCamelCase()
    {
        var (partida, _, _) = PartidaViva();
        var arena = ServicioReloj.Avanzar(partida)!.Arena!;

        var json = SerializadorMensajes.Serializar(TiposMensaje.ArenaUpdate, arena);

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        Assert.Equal("arena:update", raiz.GetProperty("type").GetString());
        var live = raiz.GetProperty("data").GetProperty("live");
        Assert.Equal(4, live.GetProperty("guardians").GetArrayLength());
        Assert.True(live.GetProperty("traps")[0].TryGetProperty("kind", out _));
        Assert.True(live.GetProperty("doors")[0].TryGetProperty("open", out _));
        Assert.Equal(1, live.GetProperty("scoreMultiplier").GetInt32());
    }

    // ---------- Publicador: orden de los mensajes ----------

    [Fact]
    [Trait("CA", "22.6")]
    public async Task Publicador_EnergiaRecogida_MandaMovimientoPuntajeYLuegoItemCollected()
    {
        var (publicador, socket, partidaId) = PublicadorConUnJugador();
        var ana = Guid.NewGuid();
        var resultado = new ResultadoEjecucion(
            partidaId, ana, ResultadoAccion.EnergiaRecogida, 3, 4, 25, EstadoPartida.EnCurso, null,
            PuntosGanados: 25, Novedades: [new ItemRecogidoNovedad(1, ana, 25)]);

        await publicador.EnviarResultadoAsync(resultado, Accion.Abajo);

        Assert.Equal(new[] { "player:moved", "score:changed", "item:collected" }, socket.Tipos().ToArray());
    }

    [Fact]
    [Trait("CA", "21.7")]
    [Trait("CA", "20.6")]
    public async Task Publicador_CaerEnTrampa_MandaPlayerFrozenATodos()
    {
        var (publicador, socket, partidaId) = PublicadorConUnJugador();
        var ana = Guid.NewGuid();
        var resultado = new ResultadoEjecucion(
            partidaId, ana, ResultadoAccion.DanoPorTrampa, 0, 1, 0, EstadoPartida.EnCurso, null,
            Novedades: [new JugadorCongeladoNovedad(ana, 3, OrigenCongelamiento.Trampa, TipoTrampa.TierraMovediza)]);

        await publicador.EnviarResultadoAsync(resultado, Accion.Abajo);

        Assert.Equal(new[] { "player:moved", "score:changed", "player:frozen" }, socket.Tipos().ToArray());
        Assert.Contains("\"trapKind\":\"QUICKSAND\"", socket.Enviados[2]);
    }

    [Fact]
    public async Task Publicador_SinNovedades_MandaLoMismoQueAntes()
    {
        var (publicador, socket, partidaId) = PublicadorConUnJugador();
        var resultado = new ResultadoEjecucion(
            partidaId, Guid.NewGuid(), ResultadoAccion.Movido, 1, 2, 0, EstadoPartida.EnCurso, null);

        await publicador.EnviarResultadoAsync(resultado, Accion.Abajo);

        Assert.Equal(new[] { "player:moved", "score:changed" }, socket.Tipos().ToArray());
    }

    // ---------- Arma paralizadora ----------

    [Fact]
    [Trait("Requisito", "Arma paralizadora")]
    public void AMensaje_ArmaDisparada_EsWeaponFiredConLosGuardianesParalizados()
    {
        var jugador = Guid.NewGuid();

        var mensaje = new ArmaDisparadaNovedad(jugador, [2, 5], 5, 15).AMensaje();

        Assert.Equal(TiposMensaje.WeaponFired, mensaje!.Value.Tipo);
        var data = Assert.IsType<ArmaDisparadaData>(mensaje.Value.Data);
        Assert.Equal(jugador, data.UserId);
        Assert.Equal(new[] { 2, 5 }, data.GuardianIds.ToArray());
        Assert.Equal((5, 15), (data.StunSeconds, data.Cooldown));
    }

    [Fact]
    [Trait("Requisito", "Arma paralizadora")]
    public void Rechazo_ArmaRecargando_TieneSuCodigo()
    {
        var jugador = Guid.NewGuid();

        var rechazo = Rechazos.Desde(jugador, ResultadoAccion.ArmaRecargando);

        Assert.Equal("ARMA_RECARGANDO", rechazo!.Code);
    }

    [Fact]
    [Trait("Requisito", "Arma paralizadora")]
    public async Task Publicador_Disparo_MandaWeaponFiredATodosSinMoverAlJugador()
    {
        var (publicador, socket, partidaId) = PublicadorConUnJugador();
        var beto = Guid.NewGuid();
        var resultado = new ResultadoEjecucion(
            partidaId, beto, ResultadoAccion.ArmaDisparada, 10, 0, 0, EstadoPartida.EnCurso, null,
            Novedades: [new ArmaDisparadaNovedad(beto, [1], 5, 15)]);

        await publicador.EnviarResultadoAsync(resultado, Accion.Extraer);

        Assert.Equal(new[] { "weapon:fired" }, socket.Tipos().ToArray());
        Assert.Contains("\"guardianIds\":[1]", socket.Enviados[0]);
    }

    [Fact]
    [Trait("Requisito", "Arma paralizadora")]
    public async Task Publicador_ArmaRecargando_RechazaSoloAlJugador()
    {
        var salas = new AdministradorSalas();
        var partidaId = Guid.NewGuid();
        var (beto, ana) = (Guid.NewGuid(), Guid.NewGuid());
        var (socketBeto, socketAna) = (new SocketFalso(), new SocketFalso());
        salas.Agregar(partidaId, new ConexionJugador(beto, socketBeto));
        salas.Agregar(partidaId, new ConexionJugador(ana, socketAna));
        var publicador = new PublicadorPartida(salas, new PartidaEnMemoriaRepositorio());
        var resultado = new ResultadoEjecucion(
            partidaId, beto, ResultadoAccion.ArmaRecargando, 10, 0, 0, EstadoPartida.EnCurso, null, Novedades: []);

        await publicador.EnviarResultadoAsync(resultado, Accion.Extraer);

        Assert.Equal(new[] { "player:rejected" }, socketBeto.Tipos().ToArray());
        Assert.Contains("ARMA_RECARGANDO", socketBeto.Enviados[0]);
        Assert.Empty(socketAna.Enviados);
    }

    [Fact]
    [Trait("Requisito", "Arma paralizadora")]
    public void EstadoPartida_MuestraRecargaDelJugadorYGuardianesParalizados()
    {
        var partida = Partida.Crear(new ConfigPartida { MaxJugadores = 2 }, Arena.DesdeFilas(
        [
            "SR........S",
            "...........",
            "...........",
            "G.........G",
            "...........",
            "..........."
        ]));
        partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar(new Random(1));
        partida.Tick(2);   // el guardián de la derecha queda a 2 pasos de Beto
        partida.Ejecutar(beto.Id, Accion.Extraer);

        var estado = PartidaVista.Desde(partida).AEstadoPartida(partida.Config.DuracionSegundos);

        Assert.Equal(15, estado.Players.Single(p => p.UserId == beto.Id).WeaponCooldown);
        Assert.Contains(estado.Live!.Guardians, g => g.StunnedSeconds == 5);
    }

    // ---------- Aceptación: una partida completa con 4 guardianes ----------

    private sealed class UsuariosEnMemoria : IUsuarioRepo
    {
        private readonly Dictionary<Guid, Usuario> _usuarios = new();
        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_usuarios.GetValueOrDefault(id));
        public Task<Usuario?> ObtenerPorUsernameAsync(string u, CancellationToken ct = default) => Task.FromResult(_usuarios.Values.FirstOrDefault(x => x.Nombre == u));
        public Task<Usuario?> ObtenerPorCorreoAsync(string c, CancellationToken ct = default) => Task.FromResult(_usuarios.Values.FirstOrDefault(x => x.Correo == c));
        public Task GuardarAsync(Usuario usuario, CancellationToken ct = default) { _usuarios[usuario.Id] = usuario; return Task.CompletedTask; }

        public async Task<Usuario> NuevoAsync(string nombre)
        {
            var usuario = new Usuario(Guid.NewGuid(), nombre, $"{nombre}@correo.com", "hash");
            usuario.MarcarVerificado();
            usuario.ElegirAvatar(Guid.NewGuid());
            await GuardarAsync(usuario);
            return usuario;
        }
    }

    [Theory]
    [InlineData("laboratorio", 1)]
    [InlineData("estacion", 2)]
    [InlineData("militar", 3)]
    [Trait("CA", "21.1")]
    [Trait("CA", "22.1")]
    [Trait("CA", "22.2")]
    [Trait("CA", "22.3")]
    [Trait("CA", "22.4")]
    [Trait("CA", "23.3")]
    public async Task Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva(string escenario, int semilla)
    {
        // Crear con escenario y 4 guardianes, unir, iniciar (como desde el menú) y dejar correr el reloj.
        var usuarios = new UsuariosEnMemoria();
        var partidas = new PartidaEnMemoriaRepositorio();
        var ana = await usuarios.NuevoAsync("Ana");
        var beto = await usuarios.NuevoAsync("Beto");

        var creada = await new ConfigurarPartida(usuarios, partidas).EjecutarAsync(new CrearPartidaComando(
            ana.Id, new ConfigPartida { EscenarioId = escenario, CantidadGuardianes = 4, DuracionSegundos = 180 }));
        await new UnirsePartida(usuarios, partidas).EjecutarAsync(new UnirsePartidaComando(creada.Id, beto.Id));
        var iniciada = await new IniciarPartida(partidas, new AzarConSemilla(semilla))
            .EjecutarAsync(new IniciarPartidaComando(creada.Id, ana.Id));

        Assert.Equal(4, iniciada.ArenaViva!.Guardianes.Count);

        var tipos = new List<string>();
        var maxGuardianes = 0;
        for (var segundo = 0; segundo < 180; segundo++)
        {
            var cambio = await partidas.UsarAsync(creada.Id, ServicioReloj.Avanzar);
            Assert.NotNull(cambio);
            tipos.AddRange(cambio.Novedades!.OfType<EventoIniciadoNovedad>().Select(e => e.Tipo.ATexto()));
            if (cambio.Arena is { } arena)
            {
                Assert.True(arena.Live.Guardians.Count >= 4);   // nunca menos de los 4 elegidos
                maxGuardianes = Math.Max(maxGuardianes, arena.Live.Guardians.Count);
            }
        }

        Assert.True(tipos.Count(t => t == "EXTRA_ENERGY") >= 3);
        Assert.True(tipos.Count(t => t == "REACTOR_OVERLOAD") >= 2);
        Assert.Contains("GUARDIAN_INVASION", tipos);
        Assert.Contains("DOUBLE_POINTS", tipos);
        Assert.Contains("ALTERNATE_ROUTES", tipos);
        Assert.Equal(9, maxGuardianes);   // 4 + 5 de la invasión
    }
}
