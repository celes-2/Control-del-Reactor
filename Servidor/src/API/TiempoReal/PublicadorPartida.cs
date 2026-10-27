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
    public const string MensajeAccion = "¡Acción!";

    /// <summary>Foto actual de la partida en formato "match:state".</summary>
    public Task<EstadoPartidaData> ObtenerEstadoAsync(Guid partidaId, CancellationToken ct = default) =>
        partidas.UsarAsync(partidaId,
            p => PartidaVista.Desde(p).AEstadoPartida(p.Config.DuracionSegundos), ct);

    public async Task EnviarEstadoAsync(Guid partidaId, CancellationToken ct = default)
    {
        var estado = await ObtenerEstadoAsync(partidaId, ct);
        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchState, estado, ct);
    }

    /// <summary>Traduce el resultado de un boton en mensajes para todos los jugadores de la partida.</summary>
    public async Task EnviarResultadoAsync(ResultadoEjecucion r, CancellationToken ct = default)
    {
        switch (r.Resultado)
        {
            case ResultadoAccion.Movido:
            case ResultadoAccion.DanoPorTrampa:
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.PlayerMoved,
                    new JugadorMovidoData(r.UsuarioId, r.X, r.Y, r.Puntaje), ct);
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.ScoreChanged,
                    new PuntajeCambiadoData(r.UsuarioId, r.Puntaje), ct);
                break;

            case ResultadoAccion.AccionRealizada:
                await salas.EnviarATodosAsync(r.PartidaId, TiposMensaje.PlayerActed,
                    new JugadorActuoData(r.UsuarioId, MensajeAccion), ct);
                break;

                // Bloqueado, Congelado, etc.: no cambio nada visible, no se avisa.
        }

        if (r.Estado == EstadoPartida.Finalizada)
            await EnviarFinAsync(r.PartidaId, ct);
    }

    public async Task EnviarFinAsync(Guid partidaId, CancellationToken ct = default)
    {
        var final = await partidas.UsarAsync(partidaId, p => PartidaVista.Desde(p).AFinal(), ct);
        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchFinished, final, ct);
    }
}