using Aplicacion.Puertos;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

/// <summary>Una acción de un jugador (un botón del control). La partida se busca a partir del usuario.</summary>
public sealed record EjecutarAccionComando(Guid UsuarioId, Accion Accion);

/// <summary>Estado del reactor visible para los clientes (WebSocket: AVAILABLE, EMPTY, DISABLED).</summary>
public enum EstadoDelReactor
{
    Disponible,
    Vacio,
    Desactivado
}

/// <summary>
/// Lo que pasó al ejecutar la acción; con esto se arman player:moved, score:changed, match:finished
/// y (Sprint 3) player:extracted / player:rejected.
/// EnergiaJugador, EnergiaReactor, EstadoReactor y PuntosGanados son del Sprint 3 y se leen DESPUÉS de ejecutar.
/// PuntosGanados es la diferencia de puntaje antes/después (+10 al extraer; negativo si cae en una trampa).
/// </summary>
public sealed record ResultadoEjecucion(
    Guid PartidaId,
    Guid UsuarioId,
    ResultadoAccion Resultado,
    int X,
    int Y,
    int Puntaje,
    EstadoPartida Estado,
    Guid? GanadorId,
    int EnergiaJugador = 0,
    int EnergiaReactor = 0,
    EstadoDelReactor EstadoReactor = EstadoDelReactor.Disponible,
    int PuntosGanados = 0);

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
            var jugador = partida.Jugadores.FirstOrDefault(j => j.Id == comando.UsuarioId);
            var puntajeAntes = jugador?.Puntaje ?? 0;

            var resultado = partida.Ejecutar(comando.UsuarioId, comando.Accion);

            jugador ??= partida.Jugadores.First(j => j.Id == comando.UsuarioId);
            var estadoReactor = EstadoReactorVista.De(partida.Reactor);

            return new ResultadoEjecucion(
                partida.Id, jugador.Id, resultado,
                jugador.Posicion.X, jugador.Posicion.Y, jugador.Puntaje,
                partida.Estado, partida.GanadorId,
                jugador.Energia, partida.Reactor.EnergiaDisponible, estadoReactor,
                jugador.Puntaje - puntajeAntes);
        }, ct);
    }
}

/// <summary>Estado visible del reactor, calculado en un solo lugar.</summary>
public static class EstadoReactorVista
{
    public static EstadoDelReactor De(Reactor reactor) =>
        reactor.Desactivado
            ? EstadoDelReactor.Desactivado
            : reactor.EnergiaDisponible <= 0
                ? EstadoDelReactor.Vacio
                : EstadoDelReactor.Disponible;
}
