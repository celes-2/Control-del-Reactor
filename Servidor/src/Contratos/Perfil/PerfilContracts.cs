namespace Contratos.Perfil;

// Contratos REST de US-04. Sin dependencias del Dominio para que Unity pueda consumirlos.
public sealed record EstadisticasDto(
    int PartidasJugadas, int Victorias, int Derrotas, int PuntosTotales, int MejorPuntaje, double PromedioPuntos);

public sealed record LogroDto(string Id, string Nombre, string Descripcion, bool Obtenido, int Progreso, int Meta);

public sealed record PerfilJugadorDto(
    Guid Id, string Username, string Email, bool EmailVerified, Guid? AvatarId,
    EstadisticasDto Estadisticas, IReadOnlyList<LogroDto> Logros);

public sealed record JugadorHistorialDto(Guid UsuarioId, string Nombre, Guid? AvatarId, int Puntaje, bool EsGanador);

/// <summary>Resultado: "Victoria", "Derrota" o "SinGanador" (visto desde el jugador que consulta).</summary>
public sealed record PartidaHistorialDto(
    Guid PartidaId, DateTime Fin, int DuracionSegundos, string ModoFin, int Prorrogas,
    string Resultado, int MiPuntaje, IReadOnlyList<JugadorHistorialDto> Jugadores);

public sealed record EntradaRankingDto(
    int Posicion, Guid UsuarioId, string Username, Guid? AvatarId, int Victorias, int Partidas, int PuntosTotales);
