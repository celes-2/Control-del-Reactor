namespace Infraestructura.Hardware;

/// <summary>
/// Descarta eventos repetidos de un control (CA-18.8). Guarda el ultimo seq aceptado de cada control:
/// un seq igual o menor es un reenvio (MQTT con QoS 1 puede entregar el mismo mensaje dos veces)
/// y se descarta antes de llegar a la partida.
/// Cuando el control se reinicia su seq vuelve a empezar: por eso Reiniciar borra su contador.
/// Se registra como Singleton (un solo filtro para todo el servidor).
/// </summary>
public sealed class FiltroDuplicados
{
    private readonly Dictionary<string, int> _ultimoSeq = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _candado = new();

    /// <summary>True si el evento es nuevo (y lo recuerda); false si es un duplicado.</summary>
    public bool EsNuevo(EventoBoton evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        lock (_candado)
        {
            if (_ultimoSeq.TryGetValue(evento.DeviceId, out var ultimo) && evento.Seq <= ultimo)
                return false;

            _ultimoSeq[evento.DeviceId] = evento.Seq;
            return true;
        }
    }

    /// <summary>El control se reinicio: su proximo seq puede volver a empezar desde abajo.</summary>
    public void Reiniciar(string deviceId)
    {
        lock (_candado)
        {
            _ultimoSeq.Remove(deviceId);
        }
    }
}