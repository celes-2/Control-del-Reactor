using Aplicacion.Puertos;
using Dominio.Historial;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

/// <summary>
/// US-04: guarda en el historial una partida que ya terminó. Lo invoca el decorador de IPartidaRepo
/// cada vez que una partida pasa a Finalizada (por el reloj o por un movimiento), así que debe ser
/// idempotente: registrar dos veces la misma partida no duplica nada.
/// </summary>
public sealed class RegistrarResultadoPartida(IHistorialRepo historial, IClock reloj)
{
    /// <returns>true si se guardó ahora; false si la partida no ha terminado o ya estaba guardada.</returns>
    public Task<bool> RegistrarAsync(PartidaVista partida, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(partida);
        if (partida.Estado != EstadoPartida.Finalizada)
            return Task.FromResult(false);

        var registro = new RegistroPartida(
            partida.Id,
            reloj.UtcNow,
            partida.SegundosTranscurridos,
            partida.ModoFin.ToString(),
            partida.Prorrogas,
            partida.GanadorId,
            partida.Jugadores
                .Select(j => new JugadorRegistro(
                    j.Id, j.Nombre, j.AvatarId == Guid.Empty ? null : j.AvatarId, j.Puntaje,
                    partida.GanadorId == j.Id))
                .ToList());

        return historial.RegistrarAsync(registro, ct);
    }
}
