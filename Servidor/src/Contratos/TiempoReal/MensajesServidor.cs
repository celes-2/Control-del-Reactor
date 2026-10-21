namespace Contratos.TiempoReal;

/// <summary>Mapa de la arena: una cadena por fila (W muro, . piso, S inicio, R reactor, T trampa).</summary>
public sealed record EscenarioData(int Width, int Height, IReadOnlyList<string> Rows);

public sealed record JugadorData(Guid UserId, string Username, Guid AvatarId, int X, int Y, int Score);

/// <summary>"match:state". TimeLeft es null en modo POINTS (no hay tiempo límite).</summary>
public sealed record EstadoPartidaData(
    Guid MatchId,
    Guid HostId,
    string Status,
    string EndMode,
    int EndValue,
    int? TimeLeft,
    int TimeElapsed,
    EscenarioData Scenario,
    IReadOnlyList<JugadorData> Players);

/// <summary>"player:moved"</summary>
public sealed record JugadorMovidoData(Guid UserId, int X, int Y, int Score);

/// <summary>"player:acted" (botón ACTION). Message es opcional.</summary>
public sealed record JugadorActuoData(Guid UserId, string? Message);

/// <summary>"score:changed"</summary>
public sealed record PuntajeCambiadoData(Guid UserId, int Score);

/// <summary>"match:tick", una vez por segundo.</summary>
public sealed record TickData(int? TimeLeft, int TimeElapsed);

/// <summary>"match:overtime": empate, se suman minutos y sigue la partida (CA-16.4).</summary>
public sealed record ProrrogaData(int ExtraMinutes, int TimeLeft);

public sealed record PuntajeFinalData(Guid UserId, string Username, int Score);

/// <summary>"match:finished". WinnerId es null si no hubo ganador.</summary>
public sealed record PartidaFinalizadaData(Guid? WinnerId, IReadOnlyList<PuntajeFinalData> Scores);

/// <summary>"error": solo se envía al jugador que lo causó.</summary>
public sealed record ErrorData(string Message);