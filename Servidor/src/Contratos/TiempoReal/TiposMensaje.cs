namespace Contratos.TiempoReal;

/// <summary>
/// Valores del campo "type" de los mensajes WebSocket.
/// Deben coincidir exactamente con Docs/contratos-websocket.md.
/// </summary>
public static class TiposMensaje
{
    // Cliente -> servidor
    public const string MatchJoin = "match:join";
    public const string MatchStart = "match:start";

    // Servidor -> cliente
    public const string MatchState = "match:state";
    public const string PlayerMoved = "player:moved";
    public const string PlayerActed = "player:acted";
    public const string ScoreChanged = "score:changed";
    public const string MatchTick = "match:tick";
    public const string MatchOvertime = "match:overtime";
    public const string MatchFinished = "match:finished";
    public const string Error = "error";
}