namespace Dominio.Comun;

/// <summary>
/// Error de regla de negocio (por ejemplo: unirse a una partida llena).
/// La capa de API lo traduce a una respuesta 400 con el mensaje.
/// </summary>
public class DominioException(string mensaje) : Exception(mensaje);
