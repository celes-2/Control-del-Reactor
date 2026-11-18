namespace Dominio.Juego;

/// <summary>Qué pasó al ejecutar una acción. La capa de tiempo real lo usa para avisar a Unity.</summary>
public enum ResultadoAccion
{
    Movido,
    Bloqueado,
    DanoPorTrampa,
    EnergiaExtraida,
    LejosDelReactor,
    ReactorVacio,
    AccionRealizada,
    Congelado,
    Enfriamiento,
    ReactorDesactivado,

    /// <summary>El jugador pisó la energía adicional de un evento y se llevó sus puntos.</summary>
    EnergiaRecogida
}
