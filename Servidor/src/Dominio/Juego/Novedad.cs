namespace Dominio.Juego;

/// <summary>De dónde viene un congelamiento.</summary>
public enum OrigenCongelamiento
{
    Guardian,
    Trampa
}

/// <summary>
/// Algo que pasó en la partida y que los clientes deben ver. La Partida las va acumulando y la capa de
/// aplicación las recoge con <see cref="Partida.TomarNovedades"/> para avisar por WebSocket.
/// </summary>
public abstract record Novedad;

public sealed record JugadorCongeladoNovedad(
    Guid JugadorId, int Segundos, OrigenCongelamiento Origen, TipoTrampa? Trampa) : Novedad;

public sealed record EventoIniciadoNovedad(Guid EventoId, TipoEvento Tipo, int Segundos) : Novedad;

public sealed record EventoTerminadoNovedad(Guid EventoId, TipoEvento Tipo) : Novedad;

public sealed record ItemRecogidoNovedad(int ItemId, Guid JugadorId, int Puntos) : Novedad;

/// <summary>
/// Un jugador disparó el arma paralizadora. GuardianesIds son los que quedaron paralizados (puede estar vacía si
/// no había ninguno cerca). SegundosRecarga: cuánto falta para que pueda volver a disparar.
/// </summary>
public sealed record ArmaDisparadaNovedad(
    Guid JugadorId, IReadOnlyList<int> GuardianesIds, int SegundosParalisis, int SegundosRecarga) : Novedad;
