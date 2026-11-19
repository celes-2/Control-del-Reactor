namespace Contratos.TiempoReal;

/// <summary>Mapa de la arena: una cadena por fila (W muro, . piso, S inicio, R reactor, T trampa).</summary>
public sealed record EscenarioData(int Width, int Height, IReadOnlyList<string> Rows);

/// <param name="FrozenSeconds">Sprint 3 (US-21): segundos que le quedan congelado; 0 = estado normal.</param>
/// <param name="WeaponCooldown">Arma paralizadora: segundos para volver a disparar; 0 = lista.</param>
public sealed record JugadorData(
    Guid UserId, string Username, Guid AvatarId, int X, int Y, int Score, int FrozenSeconds = 0, int WeaponCooldown = 0);

/// <summary>Reactor dentro de "match:state". State: AVAILABLE | EMPTY | DISABLED.</summary>
public sealed record ReactorData(int Energy, int Capacity, string State);

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
    IReadOnlyList<JugadorData> Players,
    ReactorData? Reactor = null,
    string? ScenarioId = null,
    int GuardianCount = 4,
    ArenaVivaData? Live = null);

/// <summary>"player:moved"</summary>
public sealed record JugadorMovidoData(Guid UserId, int X, int Y, int Score);

/// <summary>"player:extracted": extraccion valida, a toda la sala. ReactorState: AVAILABLE | EMPTY | DISABLED.</summary>
public sealed record JugadorExtrajoData(
    Guid UserId, int Points, int Score, int PlayerEnergy, int ReactorEnergy, string ReactorState, string? EventId);

/// <summary>"player:rejected": la extraccion no fue valida; solo al jugador que la intento.</summary>
public sealed record RechazoData(Guid UserId, string Code, string Message);

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

// ---------------------------------------------------------------- Arena viva (Sprint 3, Paquete B)

/// <summary>
/// Guardián. Temporary: llegó con la invasión y se va al terminar. Paused: acaba de congelar a alguien.
/// StunnedSeconds: segundos que sigue paralizado por el arma de un jugador (0 = normal).
/// </summary>
public sealed record GuardianData(int Id, int X, int Y, bool Temporary, bool Paused, int StunnedSeconds = 0);

/// <summary>Trampa. Kind: QUICKSAND (tierra movediza) | RISING_CELL (celda emergente) | NET (red inmovilizadora).</summary>
public sealed record TrampaData(int X, int Y, string Kind);

/// <summary>Puerta automática. Cerrada bloquea como un muro.</summary>
public sealed record PuertaData(int X, int Y, bool Open);

/// <summary>Ícono de energía adicional. El primero que lo pisa gana Points.</summary>
public sealed record ItemEnergiaData(int Id, int X, int Y, int Points, int SecondsLeft);

/// <summary>
/// Evento activo. Kind: EXTRA_ENERGY | GUARDIAN_INVASION | REACTOR_OVERLOAD | DOUBLE_POINTS |
/// ALTERNATE_ROUTES | ENERGY_RAIN.
/// </summary>
public sealed record EventoData(Guid Id, string Kind, int Duration, int SecondsLeft);

public sealed record CongeladoData(Guid UserId, int Seconds);

/// <summary>
/// Elementos que cambian solos en la arena. Viaja en "match:state" (campo live) y en "arena:update" (cada segundo).
/// Frozen lista solo a los jugadores congelados en este momento.
/// </summary>
public sealed record ArenaVivaData(
    IReadOnlyList<GuardianData> Guardians,
    IReadOnlyList<TrampaData> Traps,
    IReadOnlyList<PuertaData> Doors,
    IReadOnlyList<ItemEnergiaData> Items,
    IReadOnlyList<EventoData> Events,
    IReadOnlyList<CongeladoData> Frozen,
    int ScoreMultiplier);

/// <summary>"arena:update": foto de la arena viva, una vez por segundo mientras la partida está en curso.</summary>
public sealed record ArenaActualizadaData(int TimeElapsed, ArenaVivaData Live);

/// <summary>"event:started": Message es un texto listo para mostrar con el efecto del evento (CA-22.5).</summary>
public sealed record EventoIniciadoData(Guid EventId, string Kind, int Duration, string Message);

/// <summary>"event:ended": el evento terminó y la arena volvió a la normalidad (CA-22.7).</summary>
public sealed record EventoTerminadoData(Guid EventId, string Kind);

/// <summary>"player:frozen". Source: GUARDIAN | TRAP. TrapKind solo si Source es TRAP. Seconds: total que le queda.</summary>
public sealed record JugadorCongeladoData(Guid UserId, int Seconds, string Source, string? TrapKind);

/// <summary>"item:collected": alguien tomó la energía adicional. Llega junto con score:changed.</summary>
public sealed record ItemRecogidoData(int ItemId, Guid UserId, int Points);

/// <summary>
/// "weapon:fired": un jugador disparó el arma paralizadora. GuardianIds: los que quedaron paralizados (puede
/// venir vacía si no había ninguno cerca). Cooldown: segundos hasta que ese jugador pueda volver a disparar.
/// </summary>
public sealed record ArmaDisparadaData(Guid UserId, IReadOnlyList<int> GuardianIds, int StunSeconds, int Cooldown);
