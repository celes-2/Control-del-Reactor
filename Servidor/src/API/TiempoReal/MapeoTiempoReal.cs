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
                .Select(j => new JugadorData(j.Id, j.Nombre, j.AvatarId, j.X, j.Y, j.Puntaje, j.SegundosCongelado))
                .ToList(),
            new ReactorData(v.EnergiaReactor, v.CapacidadReactor, v.EstadoReactor.ATexto()),
            v.EscenarioId,
            v.CantidadGuardianes,
            v.AArenaViva());
    }

    /// <summary>Arena viva de la partida (campo live de match:state y data de arena:update). Null si no está activa.</summary>
    public static ArenaVivaData? AArenaViva(this PartidaVista v)
    {
        if (v.ArenaViva is not { } viva)
            return null;

        return new ArenaVivaData(
            viva.Guardianes.Select(g => new GuardianData(g.Id, g.X, g.Y, g.Temporal, g.SegundosPausa > 0)).ToList(),
            viva.Trampas.Select(t => new TrampaData(t.X, t.Y, t.Variante.ATexto())).ToList(),
            viva.Puertas.Select(p => new PuertaData(p.X, p.Y, p.Abierta)).ToList(),
            viva.Items.Select(i => new ItemEnergiaData(i.Id, i.X, i.Y, i.Puntos, i.SegundosRestantes)).ToList(),
            viva.Eventos.Select(e => new EventoData(e.Id, e.Tipo.ATexto(), e.Duracion, e.SegundosRestantes)).ToList(),
            v.Jugadores
                .Where(j => j.SegundosCongelado > 0)
                .Select(j => new CongeladoData(j.Id, j.SegundosCongelado))
                .ToList(),
            viva.MultiplicadorPuntos);
    }

    /// <summary>
    /// Traduce lo que pasó en la arena a mensajes WebSocket (type, data). Las novedades que no tienen mensaje se ignoran.
    /// </summary>
    public static (string Tipo, object Data)? AMensaje(this Novedad novedad) => novedad switch
    {
        JugadorCongeladoNovedad c => (TiposMensaje.PlayerFrozen, new JugadorCongeladoData(
            c.JugadorId, c.Segundos,
            c.Origen == OrigenCongelamiento.Guardian ? "GUARDIAN" : "TRAP",
            c.Trampa?.ATexto())),
        EventoIniciadoNovedad e => (TiposMensaje.EventStarted, new EventoIniciadoData(
            e.EventoId, e.Tipo.ATexto(), e.Segundos, MensajeDeEvento(e.Tipo, e.Segundos))),
        EventoTerminadoNovedad e => (TiposMensaje.EventEnded, new EventoTerminadoData(e.EventoId, e.Tipo.ATexto())),
        ItemRecogidoNovedad i => (TiposMensaje.ItemCollected, new ItemRecogidoData(i.ItemId, i.JugadorId, i.Puntos)),
        _ => null
    };

    /// <summary>Variante de trampa como la espera Unity.</summary>
    public static string ATexto(this TipoTrampa tipo) => tipo switch
    {
        TipoTrampa.TierraMovediza => "QUICKSAND",
        TipoTrampa.CeldaEmergente => "RISING_CELL",
        TipoTrampa.RedInmovilizadora => "NET",
        _ => tipo.ToString().ToUpperInvariant()
    };

    /// <summary>Tipo de evento como lo espera Unity.</summary>
    public static string ATexto(this TipoEvento tipo) => tipo switch
    {
        TipoEvento.EnergiaExtra => "EXTRA_ENERGY",
        TipoEvento.InvasionGuardianes => "GUARDIAN_INVASION",
        TipoEvento.SobrecargaReactor => "REACTOR_OVERLOAD",
        TipoEvento.PuntosDobles => "DOUBLE_POINTS",
        TipoEvento.RutasAlternativas => "ALTERNATE_ROUTES",
        TipoEvento.LluviaEnergia => "ENERGY_RAIN",
        _ => tipo.ToString().ToUpperInvariant()
    };

    /// <summary>Texto que Unity muestra al empezar un evento: qué pasa y cuánto dura (CA-22.5).</summary>
    public static string MensajeDeEvento(TipoEvento tipo, int segundos) => tipo switch
    {
        TipoEvento.EnergiaExtra => $"¡Energía adicional! El primero que la toque gana puntos ({segundos} s).",
        TipoEvento.InvasionGuardianes => $"¡Invasión! Llegan 5 guardianes más durante {segundos} s.",
        TipoEvento.SobrecargaReactor => $"¡Sobrecarga! El reactor no da energía durante {segundos} s.",
        TipoEvento.PuntosDobles => $"¡Puntos dobles! La energía vale el doble durante {segundos} s.",
        TipoEvento.RutasAlternativas => $"¡Rutas alternativas! Todas las puertas quedan abiertas durante {segundos} s.",
        TipoEvento.LluviaEnergia => $"¡Lluvia de energía! Aparecen varias celdas de energía durante {segundos} s.",
        _ => $"Evento especial ({segundos} s)."
    };

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