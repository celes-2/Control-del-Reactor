using Contratos.TiempoReal;
using Dominio.Comun;
using Dominio.Juego;

namespace API.TiempoReal;

/// <summary>
/// Codigos y mensajes de rechazo de una extraccion (Docs/contratos.md y reglas.md).
/// El codigo es fijo para que Unity lo compare; el mensaje se muestra tal cual al jugador.
/// </summary>
public static class Rechazos
{
    /// <summary>Rechazo para un resultado del dominio, o null si ese resultado no es un rechazo.</summary>
    public static RechazoData? Desde(Guid usuarioId, ResultadoAccion resultado) => resultado switch
    {
        ResultadoAccion.Congelado => new RechazoData(usuarioId, "JUGADOR_CONGELADO", "Estás congelado, espera a recuperarte."),
        ResultadoAccion.LejosDelReactor => new RechazoData(usuarioId, "LEJOS_DEL_REACTOR", "Acércate al reactor."),
        ResultadoAccion.ReactorDesactivado => new RechazoData(usuarioId, "REACTOR_DESACTIVADO", "El reactor está en sobrecarga."),
        ResultadoAccion.ReactorVacio => new RechazoData(usuarioId, "REACTOR_VACIO", "El reactor está vacío, espera la recarga."),
        ResultadoAccion.Enfriamiento => new RechazoData(usuarioId, "ENFRIAMIENTO", "Espera un momento."),
        _ => null
    };

    /// <summary>
    /// Rechazo para una DominioException (por su Codigo), o null si ese codigo no se le muestra al jugador.
    /// </summary>
    public static RechazoData? DesdeCodigo(Guid usuarioId, string? codigo) => codigo switch
    {
        CodigosRechazo.PartidaNoActiva => new RechazoData(usuarioId, CodigosRechazo.PartidaNoActiva, "La partida no está en curso."),
        CodigosRechazo.JugadorEliminado => new RechazoData(usuarioId, CodigosRechazo.JugadorEliminado, "Estás eliminado."),
        _ => null
    };
}