using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

// Vistas de solo lectura: fotografías de la partida que los casos de uso devuelven.
// Así la API y el tiempo real nunca reciben (ni modifican) las entidades del dominio.

/// <param name="SegundosCongelado">Sprint 3 (US-21): segundos que le quedan congelado; 0 = estado normal.</param>
/// <param name="SegundosRecargaArma">Arma paralizadora: segundos para volver a disparar; 0 = lista.</param>
public sealed record JugadorVista(
    Guid Id, string Nombre, Guid AvatarId, int X, int Y, int Vida, int Energia, bool Eliminado, int Puntaje,
    int SegundosCongelado = 0, int SegundosRecargaArma = 0);

/// <param name="Variante">Solo en las trampas (US-20): qué tipo de trampa es. Null en muros y puertas.</param>
public sealed record ObstaculoVista(int X, int Y, TipoObstaculo Tipo, TipoTrampa? Variante = null);

public sealed record PartidaVista(
    Guid Id,
    EstadoPartida Estado,
    int SegundosRestantes,
    Guid? GanadorId,
    int MaxJugadores,
    int Ancho,
    int Alto,
    int ReactorX,
    int ReactorY,
    int EnergiaReactor,
    int CapacidadReactor,
    int EnergiaObjetivo,
    IReadOnlyList<JugadorVista> Jugadores,
    IReadOnlyList<ObstaculoVista> Obstaculos,
    IReadOnlyList<string> Escenario,
    Guid AnfitrionId,
    ModoFin ModoFin,
    int PuntosObjetivo,
    int SegundosTranscurridos,
    int Prorrogas,
    EstadoDelReactor EstadoReactor = EstadoDelReactor.Disponible,
    string? EscenarioId = null,
    int CantidadGuardianes = ConfigPartida.MaximoGuardianes,
    ArenaVivaVista? ArenaViva = null)
{
    public static PartidaVista Desde(Partida p) => new(
        p.Id,
        p.Estado,
        p.SegundosRestantes,
        p.GanadorId,
        p.Config.MaxJugadores,
        p.Arena.Ancho,
        p.Arena.Alto,
        p.Reactor.Posicion.X,
        p.Reactor.Posicion.Y,
        p.Reactor.EnergiaDisponible,
        p.Reactor.Capacidad,
        p.Config.EnergiaObjetivo,
        p.Jugadores
            .Select(j => new JugadorVista(
                j.Id, j.Nombre, j.AvatarId, j.Posicion.X, j.Posicion.Y, j.Vida, j.Energia, j.Eliminado, j.Puntaje,
                j.SegundosCongelado, p.SegundosRecargaArma(j.Id)))
            .ToList(),
        p.Arena.Obstaculos
            .Select(o => new ObstaculoVista(
                o.Posicion.X, o.Posicion.Y, o.Tipo, o.Tipo == TipoObstaculo.Trampa ? o.Variante : null))
            .ToList(),
        p.Arena.ComoFilas(),
        p.Jugadores.Count > 0 ? p.Jugadores[0].Id : Guid.Empty, // el anfitrión es el primero que entró
        p.Config.ModoFin,
        p.Config.PuntosObjetivo,
        p.SegundosTranscurridos,
        p.Prorrogas,
        EstadoReactorVista.De(p.Reactor),
        p.Config.EscenarioId,
        p.Config.CantidadGuardianes,
        ArenaVivaVista.Desde(p));
}

/// <summary>Línea de la lista de partidas abiertas.</summary>
public sealed record PartidaResumen(Guid Id, int Jugadores, int MaxJugadores, string Anfitrion);

// ---------------------------------------------------------------- arena viva (Paquete B)

/// <param name="SegundosParalizado">Arma paralizadora: segundos que sigue paralizado; 0 = normal.</param>
public sealed record GuardianVista(int Id, int X, int Y, bool Temporal, int SegundosPausa, int SegundosParalizado = 0);

public sealed record TrampaVista(int X, int Y, TipoTrampa Variante);

public sealed record PuertaVista(int X, int Y, bool Abierta, int SegundosParaCambio);

/// <param name="SegundosRestantes">Lo que le queda al evento que la creó; al llegar a 0 desaparece.</param>
public sealed record ItemEnergiaVista(int Id, int X, int Y, int Puntos, int SegundosRestantes);

public sealed record EventoVista(Guid Id, TipoEvento Tipo, int Duracion, int SegundosRestantes);

/// <summary>
/// Lo que cambia solo en la arena (US-20 a US-22): guardianes, trampas, puertas, energía adicional y eventos activos.
/// Todos los jugadores reciben la misma foto (CA-20.6, CA-22.6, CA-23.4).
/// </summary>
public sealed record ArenaVivaVista(
    IReadOnlyList<GuardianVista> Guardianes,
    IReadOnlyList<TrampaVista> Trampas,
    IReadOnlyList<PuertaVista> Puertas,
    IReadOnlyList<ItemEnergiaVista> Items,
    IReadOnlyList<EventoVista> Eventos,
    int MultiplicadorPuntos)
{
    /// <summary>Null si la partida no tiene arena viva (todavía no inicia, o se inició sin ella).</summary>
    public static ArenaVivaVista? Desde(Partida p)
    {
        if (!p.ArenaVivaActiva)
            return null;

        var eventos = p.EventosActivos;
        int RestanteDe(Guid eventoId) => eventos.FirstOrDefault(e => e.Id == eventoId)?.SegundosRestantes ?? 0;

        return new ArenaVivaVista(
            p.Guardianes
                .Select(g => new GuardianVista(g.Id, g.Posicion.X, g.Posicion.Y, g.Temporal, g.SegundosPausa, g.SegundosParalizado))
                .ToList(),
            p.Arena.Obstaculos
                .Where(o => o.Tipo == TipoObstaculo.Trampa)
                .OrderBy(o => o.Posicion.Y).ThenBy(o => o.Posicion.X)
                .Select(o => new TrampaVista(o.Posicion.X, o.Posicion.Y, o.Variante))
                .ToList(),
            p.Arena.Puertas
                .OrderBy(d => d.Posicion.Y).ThenBy(d => d.Posicion.X)
                .Select(d => new PuertaVista(d.Posicion.X, d.Posicion.Y, d.Abierta, d.SegundosParaCambio))
                .ToList(),
            p.Items
                .Select(i => new ItemEnergiaVista(i.Id, i.Posicion.X, i.Posicion.Y, i.Puntos, RestanteDe(i.EventoId)))
                .ToList(),
            eventos
                .Select(e => new EventoVista(e.Id, e.Tipo, e.Duracion, e.SegundosRestantes))
                .ToList(),
            p.MultiplicadorPuntos);
    }
}
