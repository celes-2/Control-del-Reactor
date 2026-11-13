using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Parámetros que se eligen al crear una partida (módulo config-partida).
/// Es un registro inmutable: para cambiar un valor se crea una copia con "with".
/// </summary>
/// <summary>Cómo termina la partida (CA-08.4): al acabarse el tiempo o al llegar a una cantidad de puntos.</summary>
public enum ModoFin
{
    Tiempo,
    Puntos
}

public sealed record ConfigPartida
{
    public int MaxJugadores { get; init; } = 4;
    public int Ancho { get; init; } = 11;
    public int Alto { get; init; } = 11;
    public ModoFin ModoFin { get; init; } = ModoFin.Tiempo;

    /// <summary>Duración en segundos. Se usa cuando ModoFin es Tiempo.</summary>
    public int DuracionSegundos { get; init; } = 180;

    /// <summary>Puntos que dan la victoria. Se usa cuando ModoFin es Puntos.</summary>
    public int PuntosObjetivo { get; init; } = 200;

    /// <summary>Segundos que un jugador queda congelado cuando es inmovilizado (CA-08.5).</summary>
    public int SegundosCongelamiento { get; init; } = 3;

    /// <summary>Reservado. No decide el ganador: gana quien tenga más puntos.</summary>
    public int EnergiaObjetivo { get; init; } = 100;

    public int CapacidadReactor { get; init; } = 500;
    public int EnergiaPorExtraccion { get; init; } = 10;
    public int RegeneracionPorSegundo { get; init; } = 5;

    /// <summary>Puntos que gana el jugador por cada unidad de energía que realmente extrae (US-19).</summary>
    public int PuntosPorEnergia { get; init; } = 1;

    /// <summary>Segundos que debe esperar un jugador entre una extracción y la siguiente.</summary>
    public int SegundosEnfriamiento { get; init; } = 3;

    /// <summary>Puntos que pierde un jugador al caer en una trampa (nunca baja de cero).</summary>
    public int PenalizacionTrampa { get; init; } = 5;

    /// <summary>Ya no se usa: las trampas congelan y restan puntos, no quitan vida.</summary>
    public int VidaInicial { get; init; } = 100;

    /// <summary>Ya no se usa: las trampas congelan y restan puntos, no quitan vida.</summary>
    public int DanoTrampa { get; init; } = 20;

    public void Validar()
    {
        if (MaxJugadores < 2 || MaxJugadores > Arena.MaxJugadores)
            throw new DominioException($"La partida admite entre 2 y {Arena.MaxJugadores} jugadores.");
        if (Ancho < Arena.TamanoMinimo || Alto < Arena.TamanoMinimo)
            throw new DominioException($"La arena debe medir al menos {Arena.TamanoMinimo}x{Arena.TamanoMinimo}.");
        if (DuracionSegundos <= 0)
            throw new DominioException("La duración debe ser mayor que cero.");
        if (PuntosObjetivo <= 0)
            throw new DominioException("Los puntos objetivo deben ser mayores que cero.");
        if (SegundosCongelamiento < 0)
            throw new DominioException("Los segundos de congelamiento no pueden ser negativos.");
        if (!Enum.IsDefined(ModoFin))
            throw new DominioException("El modo de fin de partida no es válido.");
        if (EnergiaObjetivo <= 0)
            throw new DominioException("La energía objetivo debe ser mayor que cero.");
        if (CapacidadReactor <= 0)
            throw new DominioException("La capacidad del reactor debe ser mayor que cero.");
        if (EnergiaPorExtraccion <= 0)
            throw new DominioException("La energía por extracción debe ser mayor que cero.");
        if (RegeneracionPorSegundo < 0)
            throw new DominioException("La regeneración no puede ser negativa.");
        if (PuntosPorEnergia < 0)
            throw new DominioException("Los puntos por energía no pueden ser negativos.");
        if (SegundosEnfriamiento < 0)
            throw new DominioException("El enfriamiento no puede ser negativo.");
        if (PenalizacionTrampa < 0)
            throw new DominioException("La penalización de las trampas no puede ser negativa.");
        if (VidaInicial <= 0)
            throw new DominioException("La vida inicial debe ser mayor que cero.");
        if (DanoTrampa < 0)
            throw new DominioException("El daño de las trampas no puede ser negativo.");
    }
}