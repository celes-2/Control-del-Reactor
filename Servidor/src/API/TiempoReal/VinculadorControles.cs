using Aplicacion.Puertos;
using Contratos.TiempoReal;

namespace API.TiempoReal;

/// <summary>
/// Vincula un control con un jugador sin importar el número del control ni el orden en que entraron:
/// el jugador pide "vincular mi control" (device:pair) y el PRIMER botón que llegue de cualquier control
/// queda asociado a él (IDispositivoRepo.AsignarAsync). Ese botón solo vincula: no mueve al jugador.
/// Si hay varios jugadores esperando, se atienden en orden de petición, de uno en uno.
/// La petición vence sola a los 60 segundos. Es Singleton y guarda las peticiones en memoria.
/// </summary>
public sealed class VinculadorControles(IDispositivoRepo dispositivos, ILogger<VinculadorControles> log)
{
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(60);

    private sealed record Peticion(ConexionJugador Conexion, DateTime Vence);

    private readonly List<Peticion> _pendientes = [];
    private readonly object _candado = new();

    /// <summary>El jugador quiere vincular su control. Repetirlo reinicia su espera.</summary>
    public void Solicitar(ConexionJugador conexion)
    {
        lock (_candado)
        {
            _pendientes.RemoveAll(p => p.Conexion.UsuarioId == conexion.UsuarioId);
            _pendientes.Add(new Peticion(conexion, DateTime.UtcNow + Espera));
        }
        log.LogInformation("Jugador {UsuarioId} espera vincular su control.", conexion.UsuarioId);
    }

    /// <summary>Cancela la petición de esta conexión (el jugador se arrepintió o cerró Unity).</summary>
    public void Cancelar(ConexionJugador conexion)
    {
        lock (_candado)
            _pendientes.RemoveAll(p => ReferenceEquals(p.Conexion, conexion));
    }

    /// <summary>
    /// Se llama con cada botón nuevo de un control. Devuelve true si el botón se usó para vincular
    /// (entonces no debe llegar a la partida) y false si nadie esperaba.
    /// </summary>
    public async Task<bool> IntentarVincularAsync(string deviceId, CancellationToken ct)
    {
        Peticion? peticion = null;
        lock (_candado)
        {
            _pendientes.RemoveAll(p => p.Vence <= DateTime.UtcNow);
            if (_pendientes.Count > 0)
            {
                peticion = _pendientes[0];
                _pendientes.RemoveAt(0);
            }
        }

        if (peticion is null)
            return false;

        try
        {
            await dispositivos.AsignarAsync(deviceId, peticion.Conexion.UsuarioId, ct);
            log.LogInformation("Control {DeviceId} vinculado al jugador {UsuarioId}.", deviceId, peticion.Conexion.UsuarioId);
            await peticion.Conexion.EnviarAsync(TiposMensaje.DevicePaired, new DispositivoVinculadoData(deviceId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "No se pudo vincular el control {DeviceId} al jugador {UsuarioId}.", deviceId, peticion.Conexion.UsuarioId);
            await peticion.Conexion.EnviarAsync(TiposMensaje.Error, new ErrorData("No se pudo vincular el control. Intenta de nuevo."), ct);
        }
        return true;
    }
}
