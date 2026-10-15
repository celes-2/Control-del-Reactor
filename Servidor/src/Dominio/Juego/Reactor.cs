using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// El reactor ocupa el centro de la arena. Los jugadores le extraen energía
/// estando en una celda vecina, y el reactor se va recargando con el tiempo.
/// </summary>
public sealed class Reactor
{
    public Posicion Posicion { get; }
    public int Capacidad { get; }
    public int EnergiaDisponible { get; private set; }

    public Reactor(Posicion posicion, int capacidad)
    {
        if (capacidad <= 0)
            throw new DominioException("La capacidad del reactor debe ser mayor que cero.");

        Posicion = posicion;
        Capacidad = capacidad;
        EnergiaDisponible = capacidad;
    }

    /// <summary>Quita hasta <paramref name="solicitada"/> de energía y devuelve lo que realmente salió.</summary>
    public int Extraer(int solicitada)
    {
        if (solicitada < 0)
            throw new ArgumentOutOfRangeException(nameof(solicitada));

        var extraida = Math.Min(solicitada, EnergiaDisponible);
        EnergiaDisponible -= extraida;
        return extraida;
    }

    /// <summary>Agrega energía sin pasar de la capacidad.</summary>
    public void Recargar(int cantidad)
    {
        if (cantidad < 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad));

        EnergiaDisponible = Math.Min(Capacidad, EnergiaDisponible + cantidad);
    }

    public bool EstaAdyacente(Posicion otra) => Posicion.DistanciaManhattan(otra) == 1;
}
