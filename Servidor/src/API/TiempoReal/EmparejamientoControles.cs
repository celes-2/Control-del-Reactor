using Aplicacion.Puertos;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>Que jugador maneja cada control.</summary>
public interface IEmparejamientoControles
{
    Task<Guid?> BuscarJugadorAsync(string deviceId, CancellationToken ct = default);
}

/// <summary>
/// Plan B acordado con Celeste para la demo (mientras no exista POST /api/devices/pair):
/// pico-01 = primer jugador que entro a la partida en curso, pico-02 = el segundo.
/// Si hubiera varias partidas en curso usa la primera que encuentre (en la demo hay una sola).
/// </summary>
public sealed class EmparejamientoPorOrden(IPartidaRepo partidas) : IEmparejamientoControles
{
    public async Task<Guid?> BuscarJugadorAsync(string deviceId, CancellationToken ct = default)
    {
        var indice = IndiceDelControl(deviceId);
        if (indice is null)
            return null;

        var enCurso = await partidas.ConsultarAsync(
            p => p.Estado == EstadoPartida.EnCurso,
            p => p.Jugadores.Select(j => j.Id).ToList(),
            ct);

        var jugadores = enCurso.FirstOrDefault();
        return jugadores is not null && indice.Value < jugadores.Count ? jugadores[indice.Value] : null;
    }

    /// <summary>"pico-01" -> 0, "pico-02" -> 1. Cualquier otro nombre -> null.</summary>
    public static int? IndiceDelControl(string deviceId)
    {
        const string prefijo = "pico-";
        if (!deviceId.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(deviceId[prefijo.Length..], out var numero) && numero >= 1 ? numero - 1 : null;
    }
}