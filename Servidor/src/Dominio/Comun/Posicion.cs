namespace Dominio.Comun;

/// <summary>
/// Celda de la arena. El origen (0,0) es la esquina superior izquierda:
/// "Arriba" disminuye Y y "Abajo" lo aumenta.
/// Es un valor inmutable: dos posiciones con las mismas coordenadas son iguales.
/// </summary>
public readonly record struct Posicion(int X, int Y)
{
    public Posicion Mover(Direccion direccion) => direccion switch
    {
        Direccion.Arriba => new Posicion(X, Y - 1),
        Direccion.Abajo => new Posicion(X, Y + 1),
        Direccion.Izquierda => new Posicion(X - 1, Y),
        Direccion.Derecha => new Posicion(X + 1, Y),
        _ => throw new ArgumentOutOfRangeException(nameof(direccion))
    };

    public int DistanciaManhattan(Posicion otra) =>
        Math.Abs(X - otra.X) + Math.Abs(Y - otra.Y);
}
