namespace Dominio.Historial;

/// <summary>Un jugador dentro de una partida terminada.</summary>
public sealed record JugadorRegistro(Guid UsuarioId, string Nombre, Guid? AvatarId, int Puntaje, bool EsGanador);

/// <summary>Una partida terminada, tal como se guarda en historial-partidas.json.</summary>
public sealed record RegistroPartida(
    Guid PartidaId,
    DateTime Fin,
    int DuracionSegundos,
    string ModoFin,
    int Prorrogas,
    Guid? GanadorId,
    IReadOnlyList<JugadorRegistro> Jugadores);

/// <summary>Estadísticas históricas de un jugador. Derrotas = partidas jugadas - victorias.</summary>
public sealed record Estadisticas(int PartidasJugadas, int Victorias, int PuntosTotales, int MejorPuntaje)
{
    public int Derrotas => PartidasJugadas - Victorias;
    public double PromedioPuntos => PartidasJugadas == 0 ? 0 : Math.Round((double)PuntosTotales / PartidasJugadas, 1);

    public static Estadisticas Vacia { get; } = new(0, 0, 0, 0);

    /// <summary>Calcula las estadísticas de un jugador a partir de las partidas en las que participó.</summary>
    public static Estadisticas Calcular(Guid usuarioId, IEnumerable<RegistroPartida> partidas)
    {
        int jugadas = 0, victorias = 0, puntos = 0, mejor = 0;
        foreach (var partida in partidas)
        {
            var jugador = partida.Jugadores.FirstOrDefault(j => j.UsuarioId == usuarioId);
            if (jugador is null) continue;

            jugadas++;
            if (jugador.EsGanador) victorias++;
            puntos += jugador.Puntaje;
            mejor = Math.Max(mejor, jugador.Puntaje);
        }
        return new Estadisticas(jugadas, victorias, puntos, mejor);
    }
}

/// <summary>Un logro con su avance. Se derivan de las estadísticas: no se guardan.</summary>
public sealed record Logro(string Id, string Nombre, string Descripcion, int Progreso, int Meta)
{
    public bool Obtenido => Progreso >= Meta;

    /// <summary>Los 4 logros del Sprint 2.</summary>
    public static IReadOnlyList<Logro> Evaluar(Estadisticas e) =>
    [
        new("primera-partida", "Primera partida", "Juega tu primera partida.", Math.Min(e.PartidasJugadas, 1), 1),
        new("primera-victoria", "Primera victoria", "Gana una partida.", Math.Min(e.Victorias, 1), 1),
        new("cinco-victorias", "Cinco victorias", "Gana 5 partidas.", Math.Min(e.Victorias, 5), 5),
        new("cien-puntos", "100 puntos", "Acumula 100 puntos entre todas tus partidas.", Math.Min(e.PuntosTotales, 100), 100),
    ];
}

public enum CriterioRanking
{
    Victorias,
    Puntos
}

public sealed record EntradaRanking(
    int Posicion, Guid UsuarioId, string Nombre, Guid? AvatarId, int Victorias, int Partidas, int PuntosTotales);

public static class Ranking
{
    /// <summary>
    /// Ordena a todos los jugadores con partidas. Por victorias: más victorias, luego más puntos.
    /// Por puntos: más puntos, luego más victorias. Último desempate: nombre. El nombre y el avatar
    /// que se muestran son los de la partida más reciente del jugador.
    /// </summary>
    public static IReadOnlyList<EntradaRanking> Construir(
        IEnumerable<RegistroPartida> partidas, CriterioRanking criterio, int limite = 10)
    {
        var acumulado = new Dictionary<Guid, (string Nombre, Guid? Avatar, DateTime Visto, int Partidas, int Victorias, int Puntos)>();

        foreach (var partida in partidas)
            foreach (var j in partida.Jugadores)
            {
                acumulado.TryGetValue(j.UsuarioId, out var a);
                var masReciente = a.Nombre is null || partida.Fin >= a.Visto;
                acumulado[j.UsuarioId] = (
                    masReciente ? j.Nombre : a.Nombre,
                    masReciente ? j.AvatarId : a.Avatar,
                    masReciente ? partida.Fin : a.Visto,
                    a.Partidas + 1,
                    a.Victorias + (j.EsGanador ? 1 : 0),
                    a.Puntos + j.Puntaje);
            }

        var ordenado = criterio == CriterioRanking.Victorias
            ? acumulado.OrderByDescending(x => x.Value.Victorias).ThenByDescending(x => x.Value.Puntos)
            : acumulado.OrderByDescending(x => x.Value.Puntos).ThenByDescending(x => x.Value.Victorias);

        return ordenado
            .ThenBy(x => x.Value.Nombre, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(limite, 0))
            .Select((x, i) => new EntradaRanking(
                i + 1, x.Key, x.Value.Nombre, x.Value.Avatar, x.Value.Victorias, x.Value.Partidas, x.Value.Puntos))
            .ToList();
    }
}
