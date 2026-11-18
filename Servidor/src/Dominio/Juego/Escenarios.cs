using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>Un escenario seleccionable (US-23). Las reglas son las mismas en todos; cambian los obstáculos, rutas y guardianes.</summary>
public sealed record EscenarioDef(string Id, string Nombre, string Descripcion, IReadOnlyList<string> Filas)
{
    public int Ancho => Filas[0].Length;
    public int Alto => Filas.Count;
}

/// <summary>
/// Escenarios incluidos en el código. Mientras no exista scenarios.json, este es el catálogo.
/// W muro, . piso, S inicio de jugador, R reactor, T trampa fija, D puerta automática, G inicio de guardián.
/// Las 4 celdas vecinas al reactor son piso: sin eso nadie podría extraer (Sprint 3).
/// Todos miden 15x11, con 4 inicios (esquinas) y 4 inicios de guardián en los extremos.
/// </summary>
public static class Escenarios
{
    /// <summary>Mapa de 15x11, el mismo que usa el servidor falso de Unity. Se usa cuando no se elige escenario.</summary>
    public static IReadOnlyList<string> Predeterminado { get; } =
    [
        "WWWWWWWWWWWWWWW",
        "WS...........SW",
        "W..WW.....WW..W",
        "W..W.......W..W",
        "W.............W",
        "W......R......W",
        "W.............W",
        "W..W.......W..W",
        "W..WW.....WW..W",
        "WS...........SW",
        "WWWWWWWWWWWWWWW"
    ];

    private static readonly EscenarioDef Clasico = new(
        "clasico", "Arena clásica", "Arena abierta con muros en las esquinas interiores. Sin puertas.", Predeterminado);

    /// <summary>Los escenarios que el jugador puede elegir al crear la partida.</summary>
    public static IReadOnlyList<EscenarioDef> Catalogo { get; } =
    [
        new EscenarioDef(
            "laboratorio",
            "Laboratorio subterráneo",
            "Corredores laterales y una sala central a la que se entra por puertas automáticas.",
            [
                "WWWWWWWWWWWWWWW",
                "WS.....G.....SW",
                "W.WWWD...DWWW.W",
                "W.W.........W.W",
                "W.D.T.....T.D.W",
                "WG.....R.....GW",
                "W.D.T.....T.D.W",
                "W.W.........W.W",
                "W.WWWD...DWWW.W",
                "WS.....G.....SW",
                "WWWWWWWWWWWWWWW"
            ]),
        new EscenarioDef(
            "estacion",
            "Estación espacial",
            "Pilares alrededor del reactor y dos puertas automáticas en el anillo central.",
            [
                "WWWWWWWWWWWWWWW",
                "WS.....G.....SW",
                "W..W.......W..W",
                "W....T.....T..W",
                "W....WW.WW....W",
                "WG...D.R.D...GW",
                "W....WW.WW....W",
                "W....T.....T..W",
                "W..W.......W..W",
                "WS.....G.....SW",
                "WWWWWWWWWWWWWWW"
            ]),
        new EscenarioDef(
            "militar",
            "Complejo militar futurista",
            "Barreras con pasillos de seguridad, puertas automáticas y trampas junto al reactor.",
            [
                "WWWWWWWWWWWWWWW",
                "WS.....G.....SW",
                "W.WW.WWWWW.WW.W",
                "W..D.......D..W",
                "W.W.........W.W",
                "WG..T..R..T..GW",
                "W.W.........W.W",
                "W..D.......D..W",
                "W.WW.WWWWW.WW.W",
                "WS.....G.....SW",
                "WWWWWWWWWWWWWWW"
            ])
    ];

    /// <summary>
    /// Busca un escenario por su id (sin distinguir mayúsculas). Sin id devuelve el mapa clásico.
    /// Lanza <see cref="DominioException"/> si el id no existe.
    /// </summary>
    public static EscenarioDef Obtener(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Clasico;

        var definicion = Catalogo.FirstOrDefault(e => string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

        return definicion ?? throw new DominioException(
            $"Escenario desconocido: '{id}'. Disponibles: {string.Join(", ", Catalogo.Select(e => e.Id))}.");
    }
}
