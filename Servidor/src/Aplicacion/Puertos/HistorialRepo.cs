using Dominio.Historial;

namespace Aplicacion.Puertos;

/// <summary>Puerto de salida: guarda y consulta las partidas terminadas (historial-partidas.json).</summary>
public interface IHistorialRepo
{
    /// <summary>Guarda la partida. Devuelve false (sin cambios) si ya estaba guardada: es idempotente por PartidaId.</summary>
    Task<bool> RegistrarAsync(RegistroPartida partida, CancellationToken ct = default);

    Task<IReadOnlyList<RegistroPartida>> ListarTodasAsync(CancellationToken ct = default);

    /// <summary>Partidas en las que participó el jugador, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<RegistroPartida>> ListarDeJugadorAsync(Guid usuarioId, CancellationToken ct = default);
}
