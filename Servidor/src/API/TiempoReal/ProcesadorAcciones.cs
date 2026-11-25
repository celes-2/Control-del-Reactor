using Aplicacion;
using Aplicacion.CasosDeUso;
using Dominio.Comun;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>
/// Ejecuta la acción de un jugador y avisa a los Unity. Lo usan las dos entradas que mueven al jugador:
/// el control físico (MQTT, por ServicioControles) y el teclado de Unity (WebSocket player:input).
/// Las dos pasan por la misma regla: EjecutarAccion decide si el movimiento vale.
/// EjecutarAccion es Scoped y esta clase es Singleton: por eso se crea un scope por acción.
/// </summary>
public sealed class ProcesadorAcciones(
    PublicadorPartida publicador,
    IServiceScopeFactory scopes,
    ILogger<ProcesadorAcciones> log)
{
    public async Task ProcesarAsync(Guid usuarioId, Accion accion, string origen, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var ejecutarAccion = scope.ServiceProvider.GetRequiredService<EjecutarAccion>();

        ResultadoEjecucion resultado;
        try
        {
            resultado = await ejecutarAccion.EjecutarAsync(new EjecutarAccionComando(usuarioId, accion), ct);
        }
        catch (NoEncontradoException ex)
        {
            // El jugador no esta en ninguna partida activa: no hay a quien avisarle.
            log.LogInformation("Accion de {Origen} descartada: {Motivo}", origen, ex.Message);
            return;
        }
        catch (DominioException ex)
        {
            log.LogInformation("Accion de {Origen} descartada ({Codigo}): {Motivo}", origen, ex.Codigo, ex.Message);

            // CA-18.5: si intentaba extraer, se le explica el motivo (solo a el).
            if (accion == Accion.Extraer && Rechazos.DesdeCodigo(usuarioId, ex.Codigo) is { } rechazo)
                await publicador.EnviarRechazoAsync(usuarioId, rechazo, ct);
            return;
        }

        log.LogInformation("{Origen} -> {Accion}: {Resultado} en ({X},{Y}), puntaje {Puntaje} ({Puntos} puntos)",
            origen, accion, resultado.Resultado, resultado.X, resultado.Y, resultado.Puntaje, resultado.PuntosGanados);

        await publicador.EnviarResultadoAsync(resultado, accion, origen, ct);
    }
}
