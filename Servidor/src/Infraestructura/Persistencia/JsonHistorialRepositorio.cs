using Aplicacion.Puertos;
using Dominio.Historial;

namespace Infraestructura.Persistencia;

/// <summary>Forma de un jugador dentro de historial-partidas.json.</summary>
public sealed class JugadorHistorialRegistro
{
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = "";
    public Guid? AvatarId { get; set; }
    public int Puntaje { get; set; }
    public bool EsGanador { get; set; }
}

/// <summary>Forma de una partida terminada en historial-partidas.json.</summary>
public sealed class PartidaHistorialRegistro
{
    public Guid PartidaId { get; set; }
    public DateTime Fin { get; set; }
    public int DuracionSegundos { get; set; }
    public string ModoFin { get; set; } = "";
    public int Prorrogas { get; set; }
    public Guid? GanadorId { get; set; }
    public List<JugadorHistorialRegistro> Jugadores { get; set; } = [];

    public static PartidaHistorialRegistro Desde(RegistroPartida p) => new()
    {
        PartidaId = p.PartidaId, Fin = p.Fin, DuracionSegundos = p.DuracionSegundos, ModoFin = p.ModoFin,
        Prorrogas = p.Prorrogas, GanadorId = p.GanadorId,
        Jugadores = p.Jugadores.Select(j => new JugadorHistorialRegistro
        {
            UsuarioId = j.UsuarioId, Nombre = j.Nombre, AvatarId = j.AvatarId, Puntaje = j.Puntaje, EsGanador = j.EsGanador
        }).ToList()
    };

    public RegistroPartida ADominio() => new(
        PartidaId, Fin, DuracionSegundos, ModoFin, Prorrogas, GanadorId,
        Jugadores.Select(j => new JugadorRegistro(j.UsuarioId, j.Nombre, j.AvatarId, j.Puntaje, j.EsGanador)).ToList());
}

/// <summary>Adaptador del puerto <see cref="IHistorialRepo"/>: guarda en "historial-partidas.json".</summary>
public sealed class JsonHistorialRepositorio(JsonFileStore<PartidaHistorialRegistro> almacen) : IHistorialRepo
{
    public Task<bool> RegistrarAsync(RegistroPartida partida, CancellationToken ct = default) =>
        almacen.ModificarYObtenerAsync(todos =>
        {
            if (todos.Any(x => x.PartidaId == partida.PartidaId))
                return false;
            todos.Add(PartidaHistorialRegistro.Desde(partida));
            return true;
        }, ct);

    public async Task<IReadOnlyList<RegistroPartida>> ListarTodasAsync(CancellationToken ct = default) =>
        (await almacen.LeerAsync(ct)).Select(x => x.ADominio()).ToList();

    public async Task<IReadOnlyList<RegistroPartida>> ListarDeJugadorAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await almacen.LeerAsync(ct))
            .Where(x => x.Jugadores.Any(j => j.UsuarioId == usuarioId))
            .OrderByDescending(x => x.Fin)
            .Select(x => x.ADominio())
            .ToList();
}
