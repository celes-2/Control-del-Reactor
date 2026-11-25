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
    EnergiaRecogida,

    /// <summary>ACCIÓN lejos del reactor con arena viva: disparó el arma paralizadora (le haya pegado o no).</summary>
    ArmaDisparada,

    /// <summary>ACCIÓN lejos del reactor, pero el arma todavía se está recargando.</summary>
    ArmaRecargando
}
