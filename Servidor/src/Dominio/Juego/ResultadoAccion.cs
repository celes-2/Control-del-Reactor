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
    Congelado
}
