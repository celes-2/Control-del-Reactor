namespace Dominio.Juego;

/// <summary>
/// Escenarios incluidos en el código. Mientras no exista scenarios.json, este es el mapa por defecto.
/// W muro, . piso, S inicio de jugador, R reactor.
/// </summary>
public static class Escenarios
{
    /// <summary>Mapa de 15x11, el mismo que usa el servidor falso de Unity.</summary>
    public static IReadOnlyList<string> Predeterminado { get; } =
    [
        "WWWWWWWWWWWWWWW",
        "WS...........SW",
        "W..WW.....WW..W",
        "W..W.......W..W",
        "W......W......W",
        "W.....WRW.....W",
        "W......W......W",
        "W..W.......W..W",
        "W..WW.....WW..W",
        "WS...........SW",
        "WWWWWWWWWWWWWWW"
    ];
}
