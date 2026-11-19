using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Aplicacion;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Contratos.TiempoReal;
using Dominio.Comun;

namespace API.TiempoReal;

/// <summary>
/// Atiende la conexion WebSocket de un Unity: ws://IP:8080/ws/partida?userId=ID
/// Recibe match:join y match:start, y responde con match:state o error.
/// US-30: el móvil manda match:watch para mirar una partida como espectador, sin unirse.
/// Los movimientos NO llegan por aqui: llegan de la Pico por MQTT.
/// </summary>
public sealed class ManejadorWebSocket(
    AdministradorSalas salas,
    PublicadorPartida publicador,
    IPartidaRepo partidas,
    ILogger<ManejadorWebSocket> log)
{
    private const int TamanoMaximoMensaje = 16 * 1024;

    public async Task AtenderAsync(HttpContext contexto)
    {
        if (!contexto.WebSockets.IsWebSocketRequest)
        {
            contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
            await contexto.Response.WriteAsync("Se esperaba una conexión WebSocket.");
            return;
        }

        var identidad = contexto.RequestServices.GetRequiredService<API.Seguridad.ResolutorIdentidad>();
        if (identidad.Resolver(contexto) is not { } usuarioId)
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsync("No autorizado o credenciales inválidas.");
            return;
        }

        using var socket = await contexto.WebSockets.AcceptWebSocketAsync();
        var conexion = new ConexionJugador(usuarioId, socket);
        var ct = contexto.RequestAborted;
        Guid? partidaActual = null;
        Guid? partidaMirada = null;   // US-30: partida que mira el móvil (solo ver)

        log.LogInformation("WebSocket abierto: jugador {UsuarioId}", usuarioId);
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var texto = await RecibirTextoAsync(socket, ct);
                if (texto is null)
                    break;

                var mensaje = SerializadorMensajes.Leer(texto);
                if (mensaje is null)
                {
                    await EnviarErrorAsync(conexion, "El mensaje debe tener el formato { \"type\": ..., \"data\": ... }.", ct);
                    continue;
                }

                try
                {
                    if (mensaje.Value.Tipo == TiposMensaje.MatchWatch)
                        partidaMirada = await MirarAsync(conexion, mensaje.Value.Data, partidaMirada, ct);
                    else
                        partidaActual = await ProcesarAsync(
                            contexto.RequestServices, conexion, mensaje.Value.Tipo, mensaje.Value.Data, partidaActual, ct);
                }
                catch (Exception ex) when (ex is DominioException or NoEncontradoException)
                {
                    // Regla incumplida (partida llena, no es el anfitrion...): solo se avisa a quien lo causo.
                    await EnviarErrorAsync(conexion, ex.Message, ct);
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // El cliente se fue sin cerrar bien (por ejemplo, cerro Unity).
        }
        finally
        {
            if (partidaActual is { } id)
                salas.Quitar(id, conexion);
            if (partidaMirada is { } mirada)
                salas.QuitarEspectador(mirada, conexion);
            await CerrarAsync(socket);
            log.LogInformation("WebSocket cerrado: jugador {UsuarioId}", usuarioId);
        }
    }

    private async Task<Guid?> ProcesarAsync(
        IServiceProvider servicios, ConexionJugador conexion,
        string tipo, JsonElement data, Guid? partidaActual, CancellationToken ct)
    {
        switch (tipo)
        {
            case TiposMensaje.MatchJoin:
                var partidaId = LeerMatchId(data)
                    ?? throw new DominioException("match:join necesita data.matchId con el id de la partida.");
                await UnirAsync(servicios, conexion, partidaId, partidaActual, ct);
                return partidaId;

            case TiposMensaje.MatchStart:
                if (partidaActual is not { } actual)
                    throw new DominioException("Primero envíe match:join.");

                var iniciar = servicios.GetRequiredService<IniciarPartida>();
                await iniciar.EjecutarAsync(new IniciarPartidaComando(actual, conexion.UsuarioId), ct);
                await publicador.EnviarEstadoAsync(actual, ct);
                return actual;

            default:
                throw new DominioException($"Tipo de mensaje desconocido: '{tipo}'.");
        }
    }

    /// <summary>
    /// Si el jugador todavia no esta en la partida, lo une (UnirsePartida valida las reglas).
    /// Despues lo mete en la sala y manda match:state a todos (la lista de jugadores pudo cambiar).
    /// </summary>
    private async Task UnirAsync(
        IServiceProvider servicios, ConexionJugador conexion, Guid partidaId, Guid? anterior, CancellationToken ct)
    {
        // Lanza NoEncontradoException si la partida no existe.
        var yaEsta = await partidas.UsarAsync(partidaId,
            p => p.Jugadores.Any(j => j.Id == conexion.UsuarioId), ct);

        if (!yaEsta)
        {
            var unirse = servicios.GetRequiredService<UnirsePartida>();
            await unirse.EjecutarAsync(new UnirsePartidaComando(partidaId, conexion.UsuarioId), ct);
        }

        if (anterior is { } otra && otra != partidaId)
            salas.Quitar(otra, conexion);
        salas.Agregar(partidaId, conexion);

        await publicador.EnviarEstadoAsync(partidaId, ct);
    }

    /// <summary>
    /// US-30: el móvil mira una partida sin unirse. Entra como espectador (no reemplaza la conexión
    /// de Unity del mismo jugador) y recibe la foto actual solo él, para no molestar a los Unity.
    /// </summary>
    private async Task<Guid?> MirarAsync(
        ConexionJugador conexion, JsonElement data, Guid? anterior, CancellationToken ct)
    {
        var partidaId = LeerMatchId(data)
            ?? throw new DominioException("match:watch necesita data.matchId con el id de la partida.");

        // Lanza NoEncontradoException si la partida no existe.
        var estado = await publicador.ObtenerEstadoAsync(partidaId, ct);

        if (anterior is { } otra && otra != partidaId)
            salas.QuitarEspectador(otra, conexion);
        salas.AgregarEspectador(partidaId, conexion);

        await conexion.EnviarAsync(TiposMensaje.MatchState, estado, ct);
        return partidaId;
    }

    private static Guid? LeerMatchId(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty("matchId", out var id)
        && id.ValueKind == JsonValueKind.String
        && id.TryGetGuid(out var guid)
            ? guid
            : (Guid?)null;

    private static Task EnviarErrorAsync(ConexionJugador conexion, string mensaje, CancellationToken ct) =>
        conexion.EnviarAsync(TiposMensaje.Error, new ErrorData(mensaje), ct);

    /// <summary>Junta los pedazos de un mensaje. Devuelve null si el cliente cerro.</summary>
    private static async Task<string?> RecibirTextoAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var acumulado = new MemoryStream();

        while (true)
        {
            var resultado = await socket.ReceiveAsync(buffer.AsMemory(), ct);
            if (resultado.MessageType == WebSocketMessageType.Close)
                return null;

            acumulado.Write(buffer, 0, resultado.Count);
            if (acumulado.Length > TamanoMaximoMensaje)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Mensaje demasiado grande.", ct);
                return null;
            }

            if (resultado.EndOfMessage)
                return Encoding.UTF8.GetString(acumulado.ToArray());
        }
    }

    private static async Task CerrarAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Adiós", CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // Ya estaba cerrada.
        }
    }
}