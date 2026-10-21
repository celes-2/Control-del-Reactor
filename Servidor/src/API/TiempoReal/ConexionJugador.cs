using System.Net.WebSockets;
using System.Text;

namespace API.TiempoReal;

/// <summary>
/// Un Unity conectado. Un WebSocket solo permite un envío a la vez, y aquí pueden
/// enviar el reloj y los controles al mismo tiempo, por eso se usa un candado.
/// </summary>
public sealed class ConexionJugador(Guid usuarioId, WebSocket socket)
{
    private readonly SemaphoreSlim _candado = new(1, 1);

    public Guid UsuarioId { get; } = usuarioId;
    public WebSocket Socket { get; } = socket;

    public Task EnviarAsync<T>(string tipo, T data, CancellationToken ct = default) =>
        EnviarTextoAsync(SerializadorMensajes.Serializar(tipo, data), ct);

    public async Task EnviarTextoAsync(string texto, CancellationToken ct = default)
    {
        var bytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(texto));

        await _candado.WaitAsync(ct);
        try
        {
            if (Socket.State == WebSocketState.Open)
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        catch (WebSocketException)
        {
            // Se desconectó a mitad del envío: se ignora; el manejador lo quita de la sala.
        }
        finally
        {
            _candado.Release();
        }
    }
}