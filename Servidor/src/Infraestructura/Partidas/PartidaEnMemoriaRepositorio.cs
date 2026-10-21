using System.Collections.Concurrent;
using Aplicacion;
using Aplicacion.Puertos;
using Dominio.Juego;

namespace Infraestructura.Partidas;

/// <summary>
/// Guarda las partidas en curso en memoria. Una partida dura minutos y se reconstruye
/// al crearla, así que no se escribe en JSON. Cada partida tiene su propio candado:
/// dos acciones sobre la misma partida nunca se ejecutan a la vez, pero partidas
/// distintas sí van en paralelo.
///
/// Debe registrarse como Singleton. Las partidas se pierden si el servidor se reinicia.
/// </summary>
public sealed class PartidaEnMemoriaRepositorio : IPartidaRepo
{
    private sealed class Entrada(Partida partida)
    {
        public Partida Partida { get; } = partida;
        public SemaphoreSlim Candado { get; } = new(1, 1);
    }

    private readonly ConcurrentDictionary<Guid, Entrada> _partidas = new();

    public Task AgregarAsync(Partida partida, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(partida);

        if (!_partidas.TryAdd(partida.Id, new Entrada(partida)))
            throw new InvalidOperationException("Ya existe una partida con ese id.");

        return Task.CompletedTask;
    }

    public async Task<TResultado> UsarAsync<TResultado>(
        Guid partidaId, Func<Partida, TResultado> operacion, CancellationToken ct = default)
    {
        if (!_partidas.TryGetValue(partidaId, out var entrada))
            throw new NoEncontradoException("La partida no existe.");

        await entrada.Candado.WaitAsync(ct);
        try
        {
            return operacion(entrada.Partida);
        }
        finally
        {
            entrada.Candado.Release();
        }
    }

    public async Task<Guid?> BuscarPartidaDeJugadorAsync(Guid usuarioId, CancellationToken ct = default)
    {
        Guid? enEspera = null;

        foreach (var entrada in _partidas.Values)
        {
            await entrada.Candado.WaitAsync(ct);
            try
            {
                var partida = entrada.Partida;
                if (partida.Estado == EstadoPartida.Finalizada || partida.Jugadores.All(j => j.Id != usuarioId))
                    continue;

                if (partida.Estado == EstadoPartida.EnCurso)
                    return partida.Id;

                enEspera ??= partida.Id;
            }
            finally
            {
                entrada.Candado.Release();
            }
        }

        return enEspera;
    }

    public async Task<IReadOnlyList<TResultado>> ConsultarAsync<TResultado>(
        Func<Partida, bool> filtro, Func<Partida, TResultado> proyeccion, CancellationToken ct = default)
    {
        var resultado = new List<TResultado>();

        foreach (var entrada in _partidas.Values)
        {
            await entrada.Candado.WaitAsync(ct);
            try
            {
                if (filtro(entrada.Partida))
                    resultado.Add(proyeccion(entrada.Partida));
            }
            finally
            {
                entrada.Candado.Release();
            }
        }

        return resultado;
    }
}
