namespace Dominio.Comun;

/// <summary>
/// Error de regla de negocio (por ejemplo: unirse a una partida llena).
/// La capa de API lo traduce a una respuesta 400 con el mensaje.
/// <see cref="Codigo"/> permite distinguir el motivo sin leer el texto (Sprint 3, player:rejected).
/// </summary>
public class DominioException : Exception
{
    public string? Codigo { get; }

    public DominioException(string mensaje) : base(mensaje) { }

    public DominioException(string mensaje, string codigo) : base(mensaje) => Codigo = codigo;
}

/// <summary>Códigos de rechazo que viajan por WebSocket en player:rejected.</summary>
public static class CodigosRechazo
{
    public const string PartidaNoActiva = "PARTIDA_NO_ACTIVA";
    public const string JugadorEliminado = "JUGADOR_ELIMINADO";
    public const string JugadorAjeno = "JUGADOR_AJENO";
}
