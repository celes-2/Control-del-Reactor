using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Tablero rectangular. El reactor está en el centro y los jugadores aparecen en las esquinas.
/// Conoce los obstáculos, pero no sabe nada de jugadores ni de reglas de juego.
/// </summary>
public sealed class Arena
{
    public const int MaxJugadores = 4;
    public const int TamanoMinimo = 5;

    private readonly Dictionary<Posicion, Obstaculo> _obstaculos = new();

    public int Ancho { get; }
    public int Alto { get; }
    public Posicion PosicionReactor { get; }
    public IReadOnlyCollection<Obstaculo> Obstaculos => _obstaculos.Values;

    public Arena(int ancho, int alto, IEnumerable<Obstaculo>? obstaculos = null)
    {
        if (ancho < TamanoMinimo || alto < TamanoMinimo)
            throw new DominioException($"La arena debe medir al menos {TamanoMinimo}x{TamanoMinimo}.");

        Ancho = ancho;
        Alto = alto;
        PosicionReactor = new Posicion(ancho / 2, alto / 2);

        foreach (var obstaculo in obstaculos ?? [])
        {
            if (!EstaDentro(obstaculo.Posicion))
                throw new DominioException($"El obstáculo en {obstaculo.Posicion} queda fuera de la arena.");
            if (EsReactor(obstaculo.Posicion))
                throw new DominioException("No se puede colocar un obstáculo sobre el reactor.");
            if (EsPuntoDeAparicion(obstaculo.Posicion))
                throw new DominioException($"No se puede colocar un obstáculo en el punto de aparición {obstaculo.Posicion}.");
            if (!_obstaculos.TryAdd(obstaculo.Posicion, obstaculo))
                throw new DominioException($"Ya hay un obstáculo en {obstaculo.Posicion}.");
        }
    }

    public bool EstaDentro(Posicion posicion) =>
        posicion.X >= 0 && posicion.X < Ancho && posicion.Y >= 0 && posicion.Y < Alto;

    public bool EsReactor(Posicion posicion) => posicion == PosicionReactor;

    public Obstaculo? ObstaculoEn(Posicion posicion) =>
        _obstaculos.TryGetValue(posicion, out var obstaculo) ? obstaculo : null;

    /// <summary>Esquinas de la arena, en orden: superior izquierda, superior derecha, inferior izquierda, inferior derecha.</summary>
    public Posicion PuntoDeAparicion(int indice) => indice switch
    {
        0 => new Posicion(0, 0),
        1 => new Posicion(Ancho - 1, 0),
        2 => new Posicion(0, Alto - 1),
        3 => new Posicion(Ancho - 1, Alto - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(indice))
    };

    private bool EsPuntoDeAparicion(Posicion posicion)
    {
        for (var i = 0; i < MaxJugadores; i++)
            if (PuntoDeAparicion(i) == posicion)
                return true;
        return false;
    }
}
