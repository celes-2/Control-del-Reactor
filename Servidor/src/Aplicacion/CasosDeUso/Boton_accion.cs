using Aplicacion.Puertos;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

/// <summary>Una acción de un jugador (un botón del control). La partida se busca a partir del usuario.</summary>
public sealed record EjecutarAccionComando(Guid UsuarioId, Accion Accion);

/// <summary>Lo que pasó al ejecutar la acción; con esto se arman player:moved, score:changed y match:finished.</summary>
public sealed record ResultadoEjecucion(
    Guid PartidaId,
    Guid UsuarioId,
    ResultadoAccion Resultado,
    int X,
    int Y,
    int Puntaje,
    EstadoPartida Estado,
    Guid? GanadorId);

/// <summary>
/// Caso de uso: el jugador presionó un botón. Busca su partida y le pide al dominio que ejecute la
/// acción, con acceso exclusivo a la partida. El dominio decide si el movimiento vale.
/// Lanza <see cref="NoEncontradoException"/> si el jugador no está en una partida activa y
/// <see cref="Dominio.Comun.DominioException"/> si la partida no está en curso; quien llama
/// (el adaptador del control) los captura y descarta el evento.
/// </summary>
public sealed class EjecutarAccion(IPartidaRepo partidas)
{
    public async Task<ResultadoEjecucion> EjecutarAsync(EjecutarAccionComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var partidaId = await partidas.BuscarPartidaDeJugadorAsync(comando.UsuarioId, ct)
            ?? throw new NoEncontradoException("El jugador no está en ninguna partida activa.");

        return await partidas.UsarAsync(partidaId, partida =>
        {
            var resultado = partida.Ejecutar(comando.UsuarioId, comando.Accion);
            var jugador = partida.Jugadores.First(j => j.Id == comando.UsuarioId);

            return new ResultadoEjecucion(
                partida.Id, jugador.Id, resultado,
                jugador.Posicion.X, jugador.Posicion.Y, jugador.Puntaje,
                partida.Estado, partida.GanadorId);
        }, ct);
    }
}
