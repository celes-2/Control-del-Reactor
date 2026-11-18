using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Guardián de la arena (US-21). Persigue al jugador no congelado más cercano; si alcanza a alguien
/// lo congela y se queda quieto unos segundos. Solo la arena viva puede moverlo.
/// </summary>
public sealed class Guardian
{
    public int Id { get; }
    public Posicion Posicion { get; private set; }

    /// <summary>Los que aparecen por el evento de invasión; desaparecen cuando el evento termina.</summary>
    public bool Temporal { get; }

    /// <summary>Segundos que le faltan para dar su próximo paso.</summary>
    internal int SegundosParaPaso { get; set; }

    /// <summary>Segundos que sigue quieto después de alcanzar a un jugador.</summary>
    public int SegundosPausa { get; internal set; }

    internal Guardian(int id, Posicion posicion, bool temporal, int segundosParaPaso)
    {
        Id = id;
        Posicion = posicion;
        Temporal = temporal;
        SegundosParaPaso = segundosParaPaso;
    }

    internal void MoverA(Posicion destino) => Posicion = destino;
}
