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
