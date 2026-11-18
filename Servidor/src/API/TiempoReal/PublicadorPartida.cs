using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Contratos.TiempoReal;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>
/// Avisa a los Unity de una partida lo que paso. Lo usan los controles, el reloj y el WebSocket,
/// asi los mensajes se arman siempre igual.
/// </summary>
public sealed class PublicadorPartida(AdministradorSalas salas, IPartidaRepo partidas)
{
    /// <summary>Foto actual de la partida en formato "match:state".</summary>
    public Task<EstadoPartidaData> ObtenerEstadoAsync(Guid partidaId, CancellationToken ct = default) =>
        partidas.UsarAsync(partidaId,
            p => PartidaVista.Desde(p).AEstadoPartida(p.Config.DuracionSegundos), ct);

    public async Task EnviarEstadoAsync(Guid partidaId, CancellationToken ct = default)
    {
        var estado = await ObtenerEstadoAsync(partidaId, ct);
        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchState, estado, ct);
    }

    /// <summary>
    /// Traduce el resultado de un boton en mensajes para los jugadores de la partida.
    /// eventoId ("deviceId:seq") solo viaja en player:extracted, para poder seguir cada evento.
    /// </summary>
    public async Task EnviarResultadoAsync(
        ResultadoEjecucion r, Accion accion, string? eventoId = null, CancellationToken ct = default)
    {
        switch (r.Resultado)
        {
            case ResultadoAccion.Movido:
            case ResultadoAccion.DanoPorTrampa:
            case ResultadoAccion.EnergiaRecogida:
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.PlayerMoved,
                    new JugadorMovidoData(r.UsuarioId, r.X, r.Y, r.Puntaje), ct);
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.ScoreChanged,
                    new PuntajeCambiadoData(r.UsuarioId, r.Puntaje), ct);
                break;

            case ResultadoAccion.EnergiaExtraida:
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.PlayerExtracted, r.AExtraccion(eventoId), ct);
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.ScoreChanged,
                    new PuntajeCambiadoData(r.UsuarioId, r.Puntaje), ct);
                break;
        }

        // Rechazos: solo cuando falla una extraccion, y solo al jugador que la intento (Docs/contratos.md).
        if (accion == Accion.Extraer && Rechazos.Desde(r.UsuarioId, r.Resultado) is { } rechazo)
            await salas.EnviarAJugadorAsync(r.PartidaId, r.UsuarioId, TiposMensaje.PlayerRejected, rechazo, ct);

        // Arena viva: congelamientos, energía recogida y eventos que terminaron por esta acción (a toda la sala).
        await EnviarNovedadesAsync(r.PartidaId, r.Novedades, ct);

        if (r.Estado == EstadoPartida.Finalizada)
            await EnviarFinAsync(r.PartidaId, ct);
    }

    /// <summary>
    /// Avisa a toda la sala lo que pasó en la arena viva (player:frozen, item:collected, event:started, event:ended),
    /// en el mismo orden en que ocurrió. Así todos ven lo mismo (CA-20.6, CA-21.7, CA-22.6).
    /// </summary>
    public async Task EnviarNovedadesAsync(Guid partidaId, IReadOnlyList<Novedad>? novedades, CancellationToken ct = default)
    {
        if (novedades is null)
            return;

        foreach (var novedad in novedades)
        {
            if (novedad.AMensaje() is { } mensaje)
                await salas.EnviarATodosAsync(partidaId, mensaje.Tipo, mensaje.Data, ct);
        }
    }

    /// <summary>"arena:update": foto de guardianes, puertas, trampas, energía y eventos. La manda el reloj cada segundo.</summary>
    public Task EnviarArenaAsync(Guid partidaId, ArenaActualizadaData arena, CancellationToken ct = default) =>
        salas.EnviarATodosAsync(partidaId, TiposMensaje.ArenaUpdate, arena, ct);

    /// <summary>
    /// Rechazo que no viene de un resultado sino de una excepcion (por ejemplo, partida no activa).
    /// Se busca la partida del jugador para mandarselo solo a el.
    /// </summary>
    public async Task EnviarRechazoAsync(Guid usuarioId, RechazoData rechazo, CancellationToken ct = default)
    {
        var partidaId = await partidas.BuscarPartidaDeJugadorAsync(usuarioId, ct);
        if (partidaId is not null)
            await salas.EnviarAJugadorAsync(partidaId.Value, usuarioId, TiposMensaje.PlayerRejected, rechazo, ct);
    }

    public async Task EnviarFinAsync(Guid partidaId, CancellationToken ct = default)
    {
        var final = await partidas.UsarAsync(partidaId, p => PartidaVista.Desde(p).AFinal(), ct);
        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchFinished, final, ct);
    }
}