using Aplicacion.Puertos;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>Que jugador maneja cada control.</summary>
public interface IEmparejamientoControles
{
    Task<Guid?> BuscarJugadorAsync(string deviceId, CancellationToken ct = default);
}

/// <summary>
/// Emparejamiento real (CA-18.2): primero busca el control en dispositivos.json
/// (lo llena POST /api/devices/pair con el JWT del jugador).
/// - Control emparejado: maneja SOLO a su jugador, y solo si ese jugador esta en una partida en curso.
/// - Control sin emparejar: usa el respaldo por orden (pico-01 = primer jugador, pico-02 = segundo).
/// </summary>
public sealed class EmparejamientoPorDispositivo(
    IDispositivoRepo dispositivos,
    IPartidaRepo partidas,
    EmparejamientoPorOrden respaldo) : IEmparejamientoControles
{
    public async Task<Guid?> BuscarJugadorAsync(string deviceId, CancellationToken ct = default)
    {
        var asignado = await dispositivos.ObtenerPorDeviceIdAsync(deviceId, ct);
        if (asignado is null)
            return await respaldo.BuscarJugadorAsync(deviceId, ct);

        var enCurso = await partidas.ConsultarAsync(
            p => p.Estado == EstadoPartida.EnCurso && p.Jugadores.Any(j => j.Id == asignado.UserId),
            p => p.Id,
            ct);

        return enCurso.Count > 0 ? asignado.UserId : null;
    }
}

/// <summary>
/// Respaldo para la demo (si un control no se emparejo con POST /api/devices/pair):
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