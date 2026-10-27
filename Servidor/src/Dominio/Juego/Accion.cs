using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Las 5 acciones posibles, una por cada botón de la Raspberry Pi Pico W
/// (o de la tecla equivalente en el simulador y en Unity).
/// </summary>
public enum Accion
{
    Arriba,
    Abajo,
    Izquierda,
    Derecha,
    Extraer,

    /// <summary>Quinto botón en el MVP: no mueve ni cambia el estado, solo genera un evento visible (US-14).</summary>
    Especial
}

public static class AccionExtensiones
{
    /// <summary>Convierte una acción de movimiento en su dirección. "Extraer" no es un movimiento.</summary>
    public static Direccion ADireccion(this Accion accion) => accion switch
    {
        Accion.Arriba => Direccion.Arriba,
        Accion.Abajo => Direccion.Abajo,
        Accion.Izquierda => Direccion.Izquierda,
        Accion.Derecha => Direccion.Derecha,
        _ => throw new InvalidOperationException($"La acción {accion} no es un movimiento.")
    };
}
