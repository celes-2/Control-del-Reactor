using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Números de las reglas de la arena viva (guardianes, trampas, puertas y eventos). Tienen un valor por
/// defecto razonable; la cantidad de guardianes (2 a 4) NO está aquí: se elige al crear la partida
/// (<see cref="ConfigPartida.CantidadGuardianes"/>).
/// </summary>
public sealed record ReglasArenaViva
{
    /// <summary>Un guardián da un paso cada tantos segundos.</summary>
    public int SegundosPorPasoGuardian { get; init; } = 2;

    /// <summary>Después de alcanzar a un jugador, el guardián se queda quieto este tiempo.</summary>
    public int SegundosPausaGuardian { get; init; } = 4;

    /// <summary>Trampas que el servidor coloca al azar al empezar (mínimo 3: una de cada variante, CA-20.9).</summary>
    public int TrampasAleatorias { get; init; } = 3;

    /// <summary>Cada cuántos segundos cambia de estado una puerta.</summary>
    public int SegundosCicloPuertas { get; init; } = 6;

    /// <summary>Puntos del ícono de energía adicional para el primero que lo toca (CA-22.1).</summary>
    public int PuntosEnergiaExtra { get; init; } = 25;

    /// <summary>Vida corta del ícono: si nadie lo toma en este tiempo, desaparece.</summary>
    public int SegundosEnergiaExtra { get; init; } = 8;

    /// <summary>Guardianes que aparecen en una invasión, además de los que ya hay (CA-22.2).</summary>
    public int GuardianesInvasion { get; init; } = 5;
    public int SegundosInvasion { get; init; } = 30;

    /// <summary>Segundos que el reactor queda desactivado en cada sobrecarga (CA-22.3).</summary>
    public int SegundosSobrecarga { get; init; } = 10;
    public int SegundosPuntosDobles { get; init; } = 15;
    public int MultiplicadorPuntosDobles { get; init; } = 2;
    public int SegundosRutasAlternativas { get; init; } = 15;

    /// <summary>Celdas de energía que aparecen en la lluvia (solo en arenas sin puertas).</summary>
    public int CeldasLluviaEnergia { get; init; } = 3;
    public int PuntosCeldaLluvia { get; init; } = 10;
    public int SegundosLluviaEnergia { get; init; } = 12;

    public void Validar()
    {
        if (SegundosPorPasoGuardian < 1)
            throw new DominioException("El guardián debe tardar al menos 1 segundo por paso.");
        if (SegundosPausaGuardian < 0)
            throw new DominioException("La pausa del guardián no puede ser negativa.");
        if (TrampasAleatorias < 3 || TrampasAleatorias > 9)
            throw new DominioException("Las trampas aleatorias deben ser entre 3 y 9.");
        if (SegundosCicloPuertas < 1)
            throw new DominioException("El ciclo de las puertas debe ser de al menos 1 segundo.");
        if (PuntosEnergiaExtra < 0 || PuntosCeldaLluvia < 0)
            throw new DominioException("Los puntos de los eventos no pueden ser negativos.");
        if (GuardianesInvasion < 1)
            throw new DominioException("La invasión debe traer al menos un guardián.");
        if (SegundosEnergiaExtra < 1 || SegundosInvasion < 1 || SegundosSobrecarga < 1 ||
            SegundosPuntosDobles < 1 || SegundosRutasAlternativas < 1 || SegundosLluviaEnergia < 1)
            throw new DominioException("Los eventos deben durar al menos 1 segundo.");
        if (MultiplicadorPuntosDobles < 1)
            throw new DominioException("El multiplicador de puntos debe ser al menos 1.");
        if (CeldasLluviaEnergia < 1)
            throw new DominioException("La lluvia de energía debe traer al menos una celda.");
    }
}
