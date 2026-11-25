using System.Net.WebSockets;
using System.Text;
using API.TiempoReal;
using Aplicacion.Puertos;
using Dominio.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PruebasAPI;

/// <summary>
/// "Vincular mi control": el primer botón que llegue de cualquier control queda asociado al jugador que lo pidió,
/// sin importar el número del control ni el orden de entrada. Ese botón no llega a la partida.
/// </summary>
[Trait("CA", "18.2")]
public class VinculadorControlesTests
{
    private sealed class DispositivosFalsos : IDispositivoRepo
    {
        public Dictionary<string, Guid> Asignados { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task AsignarAsync(string deviceId, Guid userId, CancellationToken ct = default)
        {
            // Un control tiene un solo jugador y un jugador un solo control.
            foreach (var otro in Asignados.Where(p => p.Value == userId).Select(p => p.Key).ToList())
                Asignados.Remove(otro);
            Asignados[deviceId] = userId;
            return Task.CompletedTask;
        }

        public Task<DispositivoAsignado?> ObtenerPorDeviceIdAsync(string deviceId, CancellationToken ct = default) =>
            Task.FromResult<DispositivoAsignado?>(
                Asignados.TryGetValue(deviceId, out var id) ? new DispositivoAsignado(deviceId, id, DateTime.UtcNow) : null);
    }

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

    private static (VinculadorControles vinculador, DispositivosFalsos dispositivos) Crear()
    {
        var dispositivos = new DispositivosFalsos();
        return (new VinculadorControles(dispositivos, NullLogger<VinculadorControles>.Instance), dispositivos);
    }

    [Fact]
    public async Task ElPrimerBoton_VinculaElControlAlJugadorQueLoPidio_YAvisaSoloAEl()
    {
        var (vinculador, dispositivos) = Crear();
        var ana = Guid.NewGuid();
        var socketAna = new SocketFalso();
        vinculador.Solicitar(new ConexionJugador(ana, socketAna));

        // Da igual qué control sea: aquí es el pico-02 y Ana fue la primera en entrar.
        var uso = await vinculador.IntentarVincularAsync("pico-02", CancellationToken.None);

        Assert.True(uso);
        Assert.Equal(ana, dispositivos.Asignados["pico-02"]);
        var mensaje = Assert.Single(socketAna.Enviados);
        Assert.Contains("device:paired", mensaje);
        Assert.Contains("pico-02", mensaje);
    }

    [Fact]
    public async Task SinPeticion_ElBotonNoSeUsaParaVincular()
    {
        var (vinculador, dispositivos) = Crear();

        var uso = await vinculador.IntentarVincularAsync("pico-01", CancellationToken.None);

        Assert.False(uso);
        Assert.Empty(dispositivos.Asignados);
    }

    [Fact]
    public async Task DosJugadores_SeVinculanEnOrdenDePeticion_SinImportarElNumeroDelControl()
    {
        var (vinculador, dispositivos) = Crear();
        var ana = Guid.NewGuid();
        var beto = Guid.NewGuid();
        vinculador.Solicitar(new ConexionJugador(beto, new SocketFalso()));
        vinculador.Solicitar(new ConexionJugador(ana, new SocketFalso()));

        // Beto pidió primero y toma el pico-02; después Ana toma el pico-01.
        await vinculador.IntentarVincularAsync("pico-02", CancellationToken.None);
        await vinculador.IntentarVincularAsync("pico-01", CancellationToken.None);

        Assert.Equal(beto, dispositivos.Asignados["pico-02"]);
        Assert.Equal(ana, dispositivos.Asignados["pico-01"]);
    }

    [Fact]
    public async Task UnaPeticionCancelada_NoVinculaNada()
    {
        var (vinculador, dispositivos) = Crear();
        var conexion = new ConexionJugador(Guid.NewGuid(), new SocketFalso());
        vinculador.Solicitar(conexion);
        vinculador.Cancelar(conexion);

        var uso = await vinculador.IntentarVincularAsync("pico-01", CancellationToken.None);

        Assert.False(uso);
        Assert.Empty(dispositivos.Asignados);
    }

    [Fact]
    public async Task UnaPeticionSeAtiendeUnaSolaVez()
    {
        var (vinculador, dispositivos) = Crear();
        var ana = Guid.NewGuid();
        vinculador.Solicitar(new ConexionJugador(ana, new SocketFalso()));

        await vinculador.IntentarVincularAsync("pico-01", CancellationToken.None);
        var segundo = await vinculador.IntentarVincularAsync("pico-02", CancellationToken.None);

        Assert.False(segundo);   // el segundo botón ya es un botón de juego normal
        Assert.False(dispositivos.Asignados.ContainsKey("pico-02"));
    }

    [Fact]
    public async Task VincularDeNuevo_CambiaElControlDelJugador()
    {
        var (vinculador, dispositivos) = Crear();
        var ana = Guid.NewGuid();
        var conexion = new ConexionJugador(ana, new SocketFalso());

        vinculador.Solicitar(conexion);
        await vinculador.IntentarVincularAsync("pico-01", CancellationToken.None);
        vinculador.Solicitar(conexion);
        await vinculador.IntentarVincularAsync("pico-02", CancellationToken.None);

        Assert.Equal(ana, dispositivos.Asignados["pico-02"]);
        Assert.False(dispositivos.Asignados.ContainsKey("pico-01"));
    }
}
