using Aplicacion.Puertos;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

/// <summary>Consulta: el estado actual de una partida.</summary>
public sealed class ObtenerPartida(IPartidaRepo partidas)
{
    public Task<PartidaVista> EjecutarAsync(Guid partidaId, CancellationToken ct = default) =>
        partidas.UsarAsync(partidaId, partida => PartidaVista.Desde(partida), ct);
}

/// <summary>Consulta: las partidas abiertas a las que todavía se puede entrar.</summary>
public sealed class ListarPartidasEnEspera(IPartidaRepo partidas)
{
    public Task<IReadOnlyList<PartidaResumen>> EjecutarAsync(CancellationToken ct = default) =>
        partidas.ConsultarAsync(
            partida => partida.Estado == EstadoPartida.Espera,
            partida => new PartidaResumen(
                partida.Id,
                partida.Jugadores.Count,
                partida.Config.MaxJugadores,
                partida.Jugadores.FirstOrDefault()?.Nombre ?? string.Empty),
            ct);
}

/// <summary>
/// US-30: la partida activa (en espera o en curso) del jugador, o null si no está en ninguna.
/// La usa el móvil para mirar en vivo la partida que el jugador está jugando, sin unirlo a otra.
/// </summary>
public sealed class BuscarPartidaActiva(IPartidaRepo partidas)
{
    public Task<Guid?> EjecutarAsync(Guid usuarioId, CancellationToken ct = default) =>
        partidas.BuscarPartidaDeJugadorAsync(usuarioId, ct);
}