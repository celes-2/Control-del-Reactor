using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

// Vistas de solo lectura: fotografías de la partida que los casos de uso devuelven.
// Así la API y el tiempo real nunca reciben (ni modifican) las entidades del dominio.

public sealed record JugadorVista(
    Guid Id, string Nombre, Guid AvatarId, int X, int Y, int Vida, int Energia, bool Eliminado);

public sealed record ObstaculoVista(int X, int Y, TipoObstaculo Tipo);

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
    IReadOnlyList<string> Escenario)
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
                j.Id, j.Nombre, j.AvatarId, j.Posicion.X, j.Posicion.Y, j.Vida, j.Energia, j.Eliminado))
            .ToList(),
        p.Arena.Obstaculos
            .Select(o => new ObstaculoVista(o.Posicion.X, o.Posicion.Y, o.Tipo))
            .ToList(),
        p.Arena.ComoFilas());
}

/// <summary>Línea de la lista de partidas abiertas.</summary>
public sealed record PartidaResumen(Guid Id, int Jugadores, int MaxJugadores, string Anfitrion);
