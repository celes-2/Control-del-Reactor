using Aplicacion;
using Aplicacion.Puertos;
using Contratos.TiempoReal;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>Lo que cambio en una partida al pasar un segundo.</summary>
public sealed record CambioReloj(int? TimeLeft, int TimeElapsed, bool HuboProrroga, int SegundosRestantes, bool Termino);

/// <summary>
/// El reloj de las partidas del servidor real. La Partida no tiene reloj propio (es dominio puro):
/// cada segundo este servicio le llama Tick(1) y avisa a los Unity con
/// match:tick (siempre), match:overtime (empate: +2 min) y match:finished (se acabo el tiempo).
/// </summary>
public sealed class ServicioReloj(
    IPartidaRepo partidas,
    AdministradorSalas salas,
    PublicadorPartida publicador,
    ILogger<ServicioReloj> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(Intervalo);
        try
        {
            while (await reloj.WaitForNextTickAsync(stoppingToken))
                await AvanzarTodasAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // El servidor se esta apagando.
        }
    }

    private async Task AvanzarTodasAsync(CancellationToken ct)
    {
        var enCurso = await partidas.ConsultarAsync(p => p.Estado == EstadoPartida.EnCurso, p => p.Id, ct);

        foreach (var partidaId in enCurso)
        {
            try
            {
                await AvanzarAsync(partidaId, ct);
            }
            catch (NoEncontradoException)
            {
                // La partida desaparecio entre la consulta y el Tick: no pasa nada.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un error en una partida no debe detener el reloj de las demas.
                log.LogError(ex, "Error al avanzar el reloj de la partida {PartidaId}", partidaId);
            }
        }
    }

    private async Task AvanzarAsync(Guid partidaId, CancellationToken ct)
    {
        var cambio = await partidas.UsarAsync(partidaId, Avanzar, ct);
        if (cambio is null)
            return;

        await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchTick,
            new TickData(cambio.TimeLeft, cambio.TimeElapsed), ct);

        if (cambio.HuboProrroga)
        {
            log.LogInformation("Empate en la partida {PartidaId}: se suman {Segundos} s", partidaId, Partida.SegundosPorEmpate);
            await salas.EnviarATodosAsync(partidaId, TiposMensaje.MatchOvertime,
                new ProrrogaData(Partida.SegundosPorEmpate / 60, cambio.SegundosRestantes), ct);
        }

        if (cambio.Termino)
        {
            log.LogInformation("La partida {PartidaId} termino por tiempo", partidaId);
            await publicador.EnviarFinAsync(partidaId, ct);
        }
    }

    /// <summary>
    /// Avanza un segundo y dice que cambio. Es publico y no usa red para poder probarlo.
    /// Devuelve null si la partida ya no estaba en curso.
    /// </summary>
    public static CambioReloj? Avanzar(Partida partida)
    {
        if (partida.Estado != EstadoPartida.EnCurso)
            return null;

        var prorrogasAntes = partida.Prorrogas;
        partida.Tick(1);

        var porTiempo = partida.Config.ModoFin == ModoFin.Tiempo;
        return new CambioReloj(
            porTiempo ? partida.SegundosRestantes : null,   // en modo POINTS no hay tiempo limite
            partida.SegundosTranscurridos,
            partida.Prorrogas > prorrogasAntes,
            partida.SegundosRestantes,
            partida.Estado == EstadoPartida.Finalizada);
    }
}