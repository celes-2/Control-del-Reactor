using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Energía adicional (US-22, CA-22.1): un ícono en una celda de la arena. El primer jugador que pisa la celda
/// se lleva los puntos. Vive lo que dure el evento que lo creó.
/// </summary>
public sealed class ItemEnergia
{
    public int Id { get; }
    public Posicion Posicion { get; }
    public int Puntos { get; }
    public Guid EventoId { get; }

    internal ItemEnergia(int id, Posicion posicion, int puntos, Guid eventoId)
    {
        Id = id;
        Posicion = posicion;
        Puntos = puntos;
        EventoId = eventoId;
    }
}
