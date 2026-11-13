using Aplicacion.CasosDeUso;
using Contratos.TiempoReal;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>Convierte las vistas de la aplicacion en los mensajes del contrato WebSocket.</summary>
public static class MapeoTiempoReal
{
    /// <summary>"match:state". En modo POINTS no hay tiempo limite: timeLeft va en null.</summary>
    public static EstadoPartidaData AEstadoPartida(this PartidaVista v, int duracionSegundos)
    {
        var porTiempo = v.ModoFin == ModoFin.Tiempo;

        return new EstadoPartidaData(
            v.Id,
            v.AnfitrionId,
            v.Estado.ToString(),                       // Espera | EnCurso | Finalizada
            porTiempo ? "TIME" : "POINTS",
            porTiempo ? duracionSegundos : v.PuntosObjetivo,
            porTiempo ? v.SegundosRestantes : (int?)null,
            v.SegundosTranscurridos,
            new EscenarioData(v.Ancho, v.Alto, v.Escenario),
            v.Jugadores
                .Select(j => new JugadorData(j.Id, j.Nombre, j.AvatarId, j.X, j.Y, j.Puntaje))
                .ToList(),
            new ReactorData(v.EnergiaReactor, v.CapacidadReactor, v.EstadoReactor.ATexto()));
    }

    /// <summary>"match:finished": puntajes de mayor a menor.</summary>
    public static PartidaFinalizadaData AFinal(this PartidaVista v) => new(
        v.GanadorId,
        v.Jugadores
            .OrderByDescending(j => j.Puntaje)
            .Select(j => new PuntajeFinalData(j.Id, j.Nombre, j.Puntaje))
            .ToList());

    /// <summary>"player:extracted" a partir del resultado de una extraccion valida.</summary>
    public static JugadorExtrajoData AExtraccion(this ResultadoEjecucion r, string? eventoId) => new(
        r.UsuarioId,
        r.PuntosGanados,
        r.Puntaje,
        r.EnergiaJugador,
        r.EnergiaReactor,
        r.EstadoReactor.ATexto(),
        eventoId);

    /// <summary>Estado del reactor como lo espera Unity (Docs/contratos.md).</summary>
    public static string ATexto(this EstadoDelReactor estado) => estado switch
    {
        EstadoDelReactor.Disponible => "AVAILABLE",
        EstadoDelReactor.Vacio => "EMPTY",
        EstadoDelReactor.Desactivado => "DISABLED",
        _ => "AVAILABLE"
    };
}