namespace Dominio.Juego;

/// <summary>Los eventos especiales de la partida (US-22).</summary>
public enum TipoEvento
{
    /// <summary>Aparece un ícono de energía en un lugar al azar; el primero que lo toma gana los puntos.</summary>
    EnergiaExtra,

    /// <summary>Aparecen guardianes adicionales por un tiempo.</summary>
    InvasionGuardianes,

    /// <summary>El reactor se desactiva: nadie puede extraer.</summary>
    SobrecargaReactor,

    /// <summary>Creativo: los puntos que se ganan valen el doble.</summary>
    PuntosDobles,

    /// <summary>Creativo: todas las puertas quedan abiertas (rutas alternativas).</summary>
    RutasAlternativas,

    /// <summary>Creativo, solo en arenas sin puertas: aparecen varias celdas de energía a la vez.</summary>
    LluviaEnergia
}

/// <summary>Un evento que está ocurriendo ahora.</summary>
public sealed class EventoActivo
{
    public Guid Id { get; }
    public TipoEvento Tipo { get; }
    public int Duracion { get; }
    public int SegundosRestantes { get; internal set; }

    internal EventoActivo(TipoEvento tipo, int duracion)
    {
        Id = Guid.NewGuid();
        Tipo = tipo;
        Duracion = duracion;
        SegundosRestantes = duracion;
    }
}
