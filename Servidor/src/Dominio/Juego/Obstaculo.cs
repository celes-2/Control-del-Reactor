using Dominio.Comun;

namespace Dominio.Juego;

public enum TipoObstaculo
{
    /// <summary>No se puede atravesar.</summary>
    Muro,

    /// <summary>Se puede pisar, pero quita vida.</summary>
    Trampa
}

public sealed record Obstaculo(Posicion Posicion, TipoObstaculo Tipo);
