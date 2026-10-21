using Dominio.Juego;

namespace Aplicacion.Puertos;

/// <summary>
/// Puerto de salida para las partidas que están en juego.
/// Una <see cref="Partida"/> no es segura para varios hilos, y a una misma partida pueden llegar
/// acciones de varios jugadores a la vez. Por eso el repositorio no entrega la partida suelta:
/// la presta con <see cref="UsarAsync{TResultado}"/>, que da acceso exclusivo mientras dura la operación.
/// </summary>
public interface IPartidaRepo
{
    Task AgregarAsync(Partida partida, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> con acceso exclusivo a la partida.
    /// Lanza <see cref="NoEncontradoException"/> si la partida no existe.
    /// </summary>
    Task<TResultado> UsarAsync<TResultado>(
        Guid partidaId, Func<Partida, TResultado> operacion, CancellationToken ct = default);

    /// <summary>
    /// Id de la partida activa (en espera o en curso) en la que está el jugador, o null si no está en ninguna.
    /// Si estuviera en varias, prefiere la que ya está en curso. Lo usa quien recibe acciones sueltas
    /// (por ejemplo el botón de un control) y solo conoce al usuario.
    /// </summary>
    Task<Guid?> BuscarPartidaDeJugadorAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Recorre las partidas que cumplen el filtro y devuelve una proyección de cada una.</summary>
    Task<IReadOnlyList<TResultado>> ConsultarAsync<TResultado>(
        Func<Partida, bool> filtro, Func<Partida, TResultado> proyeccion, CancellationToken ct = default);
}
