using Infraestructura.Hardware;

namespace API.TiempoReal;

/// <summary>
/// Escucha los controles mientras el servidor este encendido.
/// Por ahora solo registra cada boton en la consola. Cuando EjecutarAccion este en develop,
/// aqui se busca el jugador del control, se ejecuta la accion y se avisa por WebSocket.
/// </summary>
public sealed class ServicioControles(IFuenteEventosBoton fuente, ILogger<ServicioControles> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        fuente.EscucharAsync(ProcesarAsync, stoppingToken);

    private Task ProcesarAsync(EventoBoton evento)
    {
        log.LogInformation("Boton {Accion} del control {DeviceId} (seq {Seq})",
            evento.Accion, evento.DeviceId, evento.Seq);
        return Task.CompletedTask;
    }
}