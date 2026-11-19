namespace Contratos.TiempoReal;

/// <summary>
/// Valores del campo "type" de los mensajes WebSocket.
/// Deben coincidir exactamente con Docs/contratos.md.
/// </summary>
public static class TiposMensaje
{
    // Cliente -> servidor
    public const string MatchJoin = "match:join";
    public const string MatchStart = "match:start";

    // Servidor -> cliente
    public const string MatchState = "match:state";
    public const string PlayerMoved = "player:moved";
    public const string PlayerExtracted = "player:extracted";
    public const string PlayerRejected = "player:rejected";
    public const string ScoreChanged = "score:changed";
    public const string MatchTick = "match:tick";
    public const string MatchOvertime = "match:overtime";
    public const string MatchFinished = "match:finished";

    // Arena viva (Sprint 3, Paquete B: US-20 a US-22)
    public const string ArenaUpdate = "arena:update";
    public const string EventStarted = "event:started";
    public const string EventEnded = "event:ended";
    public const string PlayerFrozen = "player:frozen";
    public const string ItemCollected = "item:collected";
    public const string WeaponFired = "weapon:fired";
    public const string Error = "error";
}