namespace Aplicacion.Puertos;

/// <summary>
/// De dónde sale el azar de una partida (posición de las trampas, momento de los eventos, celdas de energía).
/// En producción cada partida usa un <see cref="Random"/> nuevo; en las pruebas se inyecta uno con semilla fija
/// para que el resultado sea siempre el mismo.
/// </summary>
public interface IFuenteAzar
{
    /// <summary>Un generador para una partida. Cada partida recibe el suyo (Random no es seguro entre hilos).</summary>
    Random Nuevo();
}

/// <summary>Azar con semilla fija: la misma semilla produce siempre la misma partida.</summary>
public sealed class AzarConSemilla(int semilla) : IFuenteAzar
{
    public Random Nuevo() => new(semilla);
}
