using Dominio.Comun;

namespace Dominio.Juego;

public enum TipoObstaculo
{
    /// <summary>No se puede atravesar.</summary>
    Muro,

    /// <summary>Se puede pisar, pero inmoviliza al jugador y le quita puntos (ver <see cref="EfectoTrampa"/>).</summary>
    Trampa,

    /// <summary>Se puede cruzar solo mientras está abierta. Abre y cierra sola (ver <see cref="Puerta"/>).</summary>
    Puerta
}

/// <summary>Las tres variantes de trampa (US-20, CA-20.9). Todas inmovilizan; cambian la duración y la penalización.</summary>
public enum TipoTrampa
{
    /// <summary>La base: congela lo que dice la configuración y resta la penalización normal.</summary>
    TierraMovediza,

    /// <summary>Una celda surge del piso: congela 2 segundos más.</summary>
    CeldaEmergente,

    /// <summary>Una red atrapa al avatar poco tiempo: congela 1 segundo menos.</summary>
    RedInmovilizadora
}

/// <param name="Variante">Solo tiene sentido cuando <paramref name="Tipo"/> es Trampa.</param>
public sealed record Obstaculo(
    Posicion Posicion, TipoObstaculo Tipo, TipoTrampa Variante = TipoTrampa.TierraMovediza);

/// <summary>
/// Efecto de cada variante de trampa, calculado a partir de la configuración de la partida.
/// Todas restan lo mismo (Config.PenalizacionTrampa, 5 puntos); solo cambia cuánto inmovilizan.
/// </summary>
public static class EfectoTrampa
{
    public static int Segundos(TipoTrampa tipo, ConfigPartida config) => tipo switch
    {
        TipoTrampa.CeldaEmergente => config.SegundosCongelamiento + 2,
        TipoTrampa.RedInmovilizadora => Math.Max(0, config.SegundosCongelamiento - 1),
        _ => config.SegundosCongelamiento
    };

    /// <summary>Igual para las tres variantes: la regla del juego es restar 5 puntos (mínimo 0).</summary>
    public static int Penalizacion(TipoTrampa tipo, ConfigPartida config) => config.PenalizacionTrampa;
}
