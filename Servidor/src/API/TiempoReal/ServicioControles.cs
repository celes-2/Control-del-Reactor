using Aplicacion;
using Aplicacion.CasosDeUso;
using Dominio.Comun;
using Infraestructura.Hardware;

namespace API.TiempoReal;

/// <summary>
/// Escucha los controles mientras el servidor este encendido. Por cada boton:
/// busca el jugador del control, ejecuta la accion (EjecutarAccion) y avisa a los Unity.
/// EjecutarAccion es Scoped y este servicio es Singleton: por eso se crea un scope por evento.
/// </summary>
public sealed class ServicioControles(
    IFuenteEventosBoton fuente,
    IEmparejamientoControles emparejamiento,
    PublicadorPartida publicador,
    IServiceScopeFactory scopes,
    ILogger<ServicioControles> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        fuente.EscucharAsync(evento => ProcesarAsync(evento, stoppingToken), stoppingToken);

    private async Task ProcesarAsync(EventoBoton evento, CancellationToken ct)
    {
        var usuarioId = await emparejamiento.BuscarJugadorAsync(evento.DeviceId, ct);
        if (usuarioId is null)
        {
            log.LogInformation("Boton {Accion} de {DeviceId} descartado: no hay jugador para ese control en una partida en curso.",
                evento.Accion, evento.DeviceId);
            return;
        }

        using var scope = scopes.CreateScope();
        var ejecutarAccion = scope.ServiceProvider.GetRequiredService<EjecutarAccion>();

        ResultadoEjecucion resultado;
        try
        {
            resultado = await ejecutarAccion.EjecutarAsync(
                new EjecutarAccionComando(usuarioId.Value, evento.Accion), ct);
        }
        catch (Exception ex) when (ex is NoEncontradoException or DominioException)
        {
            // Acordado con Celeste: si el jugador no esta en partida o la partida no esta en curso, se descarta.
            log.LogInformation("Boton de {DeviceId} descartado: {Motivo}", evento.DeviceId, ex.Message);
            return;
        }

        log.LogInformation("{DeviceId} -> {Accion}: {Resultado} en ({X},{Y}), puntaje {Puntaje}",
            evento.DeviceId, evento.Accion, resultado.Resultado, resultado.X, resultado.Y, resultado.Puntaje);

        await publicador.EnviarResultadoAsync(resultado, ct);
    }
}