namespace Contratos.Partidas;

// Contratos HTTP de las partidas: solo tipos simples (números, texto, Guid), sin entidades del dominio.
// Son los mismos nombres que leerá el cliente de Unity.

public sealed record ObstaculoSolicitud(int X, int Y, string Tipo); // Tipo: "Muro" o "Trampa"

/// <summary>
/// Todos los parámetros de la partida son opcionales: lo que no se envíe usa el valor por defecto.
/// OJO: UsuarioId viaja en el cuerpo solo mientras no exista el login con JWT.
/// Cuando esté listo, saldrá del token y este campo se eliminará.
/// </summary>
public sealed record CrearPartidaSolicitud(
    Guid UsuarioId,
    int? MaxJugadores = null,
    int? Ancho = null,
    int? Alto = null,
    int? DuracionSegundos = null,
    int? EnergiaObjetivo = null,
    int? CapacidadReactor = null,
    int? EnergiaPorExtraccion = null,
    int? RegeneracionPorSegundo = null,
    int? VidaInicial = null,
    int? DanoTrampa = null,
    IReadOnlyList<ObstaculoSolicitud>? Obstaculos = null);

public sealed record UnirsePartidaSolicitud(Guid UsuarioId);

public sealed record IniciarPartidaSolicitud(Guid UsuarioId);

public sealed record JugadorRespuesta(
    Guid Id, string Nombre, Guid AvatarId, int X, int Y, int Vida, int Energia, bool Eliminado);

public sealed record ObstaculoRespuesta(int X, int Y, string Tipo);

public sealed record PartidaRespuesta(
    Guid Id,
    string Estado,
    int SegundosRestantes,
    Guid? GanadorId,
    int MaxJugadores,
    int Ancho,
    int Alto,
    int ReactorX,
    int ReactorY,
    int EnergiaReactor,
    int CapacidadReactor,
    int EnergiaObjetivo,
    IReadOnlyList<JugadorRespuesta> Jugadores,
    IReadOnlyList<ObstaculoRespuesta> Obstaculos,
    IReadOnlyList<string> Escenario); // filas de texto: W muro, . piso, S inicio, R reactor, T trampa

public sealed record PartidaResumenRespuesta(Guid Id, int Jugadores, int MaxJugadores, string Anfitrion);
