using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Un usuario dentro de una partida concreta. Su Id es el mismo del usuario.
/// Los métodos que cambian su estado son "internal": solo la Partida puede llamarlos,
/// así nadie puede, por ejemplo, regalarse energía desde fuera del dominio.
/// </summary>
public sealed class Jugador
{
    public Guid Id { get; }
    public string Nombre { get; }
    public Guid AvatarId { get; }
    public Posicion Posicion { get; private set; }
    public int Vida { get; private set; }
    public int Energia { get; private set; }
    public int Puntaje { get; private set; }

    /// <summary>Segundos que le quedan congelado. Mientras sea mayor que cero no puede actuar.</summary>
    public int SegundosCongelado { get; private set; }
    public bool Congelado => SegundosCongelado > 0;
    public bool Eliminado => Vida <= 0;

    internal Jugador(Guid id, string nombre, Guid avatarId, Posicion posicion, int vida)
    {
        Id = id;
        Nombre = nombre;
        AvatarId = avatarId;
        Posicion = posicion;
        Vida = vida;
    }

    internal void MoverA(Posicion destino) => Posicion = destino;

    internal void SumarPunto() => Puntaje++;

    internal void Congelar(int segundos) => SegundosCongelado = Math.Max(SegundosCongelado, segundos);

    internal void AvanzarTiempo(int segundos) => SegundosCongelado = Math.Max(0, SegundosCongelado - segundos);

    internal void GanarEnergia(int cantidad) => Energia += cantidad;

    internal void RecibirDano(int dano) => Vida = Math.Max(0, Vida - dano);

    /// <summary>Deja la energía en cero y devuelve la que tenía.</summary>
    internal int VaciarEnergia()
    {
        var energia = Energia;
        Energia = 0;
        return energia;
    }
}
