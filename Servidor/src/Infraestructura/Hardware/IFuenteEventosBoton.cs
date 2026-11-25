namespace Infraestructura.Hardware;

/// <summary>
/// De donde llegan los botones de los controles. Hoy es MQTT; manana podria ser
/// un simulador o REST sin cambiar a quien lo usa (principio de Liskov).
/// </summary>
public interface IFuenteEventosBoton
{
    /// <summary>
    /// Escucha hasta que se cancele. Por cada evento valido llama a alRecibir.
    /// Cuando un control se acaba de conectar (se reinicio) llama a alConectarse con su deviceId.
    /// </summary>
    Task EscucharAsync(Func<EventoBoton, Task> alRecibir, Func<string, Task> alConectarse, CancellationToken ct);
}