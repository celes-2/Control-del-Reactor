using Aplicacion.Puertos;
using Contratos.Perfil;
using Dominio.Historial;

namespace Aplicacion.CasosDeUso;

/// <summary>US-04: perfil del jugador con estadísticas, logros, historial de partidas y ranking.</summary>
public sealed class PerfilJugadorService(IUsuarioRepo usuarios, IHistorialRepo historial)
{
    public const int LimiteMaximo = 100;

    public async Task<PerfilJugadorDto> ObtenerPerfilAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, ct)
            ?? throw new AuthException("USUARIO_NO_ENCONTRADO", "No se encontró el usuario.");

        var partidas = await historial.ListarDeJugadorAsync(usuarioId, ct);
        var e = Estadisticas.Calcular(usuarioId, partidas);

        return new PerfilJugadorDto(
            usuario.Id, usuario.Nombre, usuario.Correo, usuario.Verificado, usuario.AvatarId,
            new EstadisticasDto(e.PartidasJugadas, e.Victorias, e.Derrotas, e.PuntosTotales, e.MejorPuntaje, e.PromedioPuntos),
            Logro.Evaluar(e).Select(l => new LogroDto(l.Id, l.Nombre, l.Descripcion, l.Obtenido, l.Progreso, l.Meta)).ToList());
    }

    public async Task<IReadOnlyList<PartidaHistorialDto>> ObtenerHistorialAsync(
        Guid usuarioId, int limite = 20, CancellationToken ct = default)
    {
        var partidas = await historial.ListarDeJugadorAsync(usuarioId, ct);
        return partidas
            .Take(Math.Clamp(limite, 1, LimiteMaximo))
            .Select(p =>
            {
                var yo = p.Jugadores.First(j => j.UsuarioId == usuarioId);
                var resultado = p.GanadorId is null ? "SinGanador" : yo.EsGanador ? "Victoria" : "Derrota";
                return new PartidaHistorialDto(
                    p.PartidaId, p.Fin, p.DuracionSegundos, p.ModoFin, p.Prorrogas, resultado, yo.Puntaje,
                    p.Jugadores.Select(j => new JugadorHistorialDto(j.UsuarioId, j.Nombre, j.AvatarId, j.Puntaje, j.EsGanador)).ToList());
            })
            .ToList();
    }

    /// <param name="criterio">"victorias" o "puntos" (sin distinguir mayúsculas).</param>
    public async Task<IReadOnlyList<EntradaRankingDto>> ObtenerRankingAsync(
        string? criterio, int limite = 10, CancellationToken ct = default)
    {
        var elegido = (criterio ?? "victorias").Trim().ToLowerInvariant() switch
        {
            "victorias" => CriterioRanking.Victorias,
            "puntos" => CriterioRanking.Puntos,
            _ => throw new AuthException("CRITERIO_INVALIDO", "El criterio del ranking debe ser \"victorias\" o \"puntos\".")
        };

        var todas = await historial.ListarTodasAsync(ct);
        return Ranking.Construir(todas, elegido, Math.Clamp(limite, 1, LimiteMaximo))
            .Select(x => new EntradaRankingDto(x.Posicion, x.UsuarioId, x.Nombre, x.AvatarId, x.Victorias, x.Partidas, x.PuntosTotales))
            .ToList();
    }
}
