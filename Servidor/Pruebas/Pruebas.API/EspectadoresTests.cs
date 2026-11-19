using System.Net.WebSockets;
using System.Text;
using API.TiempoReal;
using Contratos.TiempoReal;
using Xunit;

namespace PruebasAPI;

/// <summary>US-30: el móvil mira la partida como espectador sin quitarle la conexión a Unity.</summary>
public class EspectadoresTests
{
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
    }

    [Fact]
    public async Task EnviarATodos_LlegaAlJugadorYAlEspectador()
    {
        var salas = new AdministradorSalas();
        var partidaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var unity = new SocketFalso();
        var celular = new SocketFalso();
        salas.Agregar(partidaId, new ConexionJugador(usuarioId, unity));
        salas.AgregarEspectador(partidaId, new ConexionJugador(usuarioId, celular));

        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchTick, new TickData(60, 0));

        Assert.Single(unity.Enviados);
        Assert.Single(celular.Enviados);
    }

    [Fact]
    public async Task Espectador_DelMismoUsuario_NoLeQuitaLaConexionAUnity()
    {
        var salas = new AdministradorSalas();
        var partidaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var unity = new SocketFalso();
        var celular = new SocketFalso();
        salas.Agregar(partidaId, new ConexionJugador(usuarioId, unity));
        salas.AgregarEspectador(partidaId, new ConexionJugador(usuarioId, celular));

        // Los mensajes privados (rechazos) siguen llegando a Unity y no al celular.
        await salas.EnviarAJugadorAsync(partidaId, usuarioId, TiposMensaje.PlayerRejected,
            new RechazoData(usuarioId, "REACTOR_VACIO", "El reactor no tiene energía."));

        Assert.Single(unity.Enviados);
        Assert.Empty(celular.Enviados);
    }

    [Fact]
    public async Task QuitarEspectador_DejaDeRecibirMensajes()
    {
        var salas = new AdministradorSalas();
        var partidaId = Guid.NewGuid();
        var celular = new SocketFalso();
        var conexion = new ConexionJugador(Guid.NewGuid(), celular);
        salas.AgregarEspectador(partidaId, conexion);

        salas.QuitarEspectador(partidaId, conexion);
        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchTick, new TickData(60, 0));

        Assert.Empty(celular.Enviados);
    }

    [Fact]
    public async Task Espectador_RecibeAunqueNoHayaJugadoresConectados()
    {
        var salas = new AdministradorSalas();
        var partidaId = Guid.NewGuid();
        var celular = new SocketFalso();
        salas.AgregarEspectador(partidaId, new ConexionJugador(Guid.NewGuid(), celular));

        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchTick, new TickData(60, 0));

        Assert.Single(celular.Enviados);
    }
}