using Aplicacion;
using Aplicacion.CasosDeUso;
using Dominio.Comun;
using Infraestructura.Hardware;

namespace API.TiempoReal;

/// <summary>
/// Escucha los controles mientras el servidor este encendido. Por cada boton:
/// descarta duplicados (FiltroDuplicados), busca el jugador del control, ejecuta la accion
/// (EjecutarAccion) y avisa a los Unity. Cuando un control se conecta, reinicia su contador de seq.
/// EjecutarAccion es Scoped y este servicio es Singleton: por eso se crea un scope por evento.
/// </summary>
public sealed class ServicioControles(
    IFuenteEventosBoton fuente,
    FiltroDuplicados filtro,
    IEmparejamientoControles emparejamiento,
    PublicadorPartida publicador,
    IServiceScopeFactory scopes,
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

        log.LogInformation("{DeviceId} -> {Accion} (seq {Seq}): {Resultado} en ({X},{Y}), puntaje {Puntaje}",
            evento.DeviceId, evento.Accion, evento.Seq, resultado.Resultado, resultado.X, resultado.Y, resultado.Puntaje);

        await publicador.EnviarResultadoAsync(resultado, ct);
    }
}