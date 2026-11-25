using Dominio.Juego;
using Infraestructura.Hardware;

namespace API.TiempoReal;

/// <summary>
/// Escucha los controles mientras el servidor este encendido. Por cada boton:
/// descarta duplicados (FiltroDuplicados), busca el jugador del control, ejecuta la accion
/// (ProcesadorAcciones) y avisa a los Unity. Cuando un control se conecta, reinicia su contador de seq.
/// </summary>
public sealed class ServicioControles(
    IFuenteEventosBoton fuente,
    FiltroDuplicados filtro,
    IEmparejamientoControles emparejamiento,
    ProcesadorAcciones procesador,
    VinculadorControles vinculador,
    ILogger<ServicioControles> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        fuente.EscucharAsync(
            evento => ProcesarAsync(evento, stoppingToken),
            ControlConectadoAsync,
            stoppingToken);

    /// <summary>La Pico se acaba de conectar (se reinicio): su seq vuelve a empezar desde 1.</summary>
    private Task ControlConectadoAsync(string deviceId)
    {
        filtro.Reiniciar(deviceId);
        log.LogInformation("Control {DeviceId} en linea: se reinicia su contador de seq.", deviceId);
        return Task.CompletedTask;
    }

    private async Task ProcesarAsync(EventoBoton evento, CancellationToken ct)
    {
        // CA-18.8: un reenvio del mismo evento fisico no debe llegar a la partida.
        if (!filtro.EsNuevo(evento))
        {
            log.LogInformation("Boton {Accion} de {DeviceId} descartado: seq {Seq} repetido o viejo.",
                evento.Accion, evento.DeviceId, evento.Seq);
            return;
        }

        // Un jugador pidió "vincular mi control": este botón solo sirve para eso y no mueve a nadie.
        if (await vinculador.IntentarVincularAsync(evento.DeviceId, ct))
        {
            log.LogInformation("Boton de {DeviceId} usado para vincular el control a un jugador.", evento.DeviceId);
            return;
        }

        var usuarioId = await emparejamiento.BuscarJugadorAsync(evento.DeviceId, ct);
        if (usuarioId is null)
        {
            log.LogInformation("Boton {Accion} de {DeviceId} descartado: no hay jugador para ese control en una partida en curso.",
                evento.Accion, evento.DeviceId);
            return;
        }

        await procesador.ProcesarAsync(usuarioId.Value, evento.Accion, $"{evento.DeviceId}:{evento.Seq}", ct);
    }
}