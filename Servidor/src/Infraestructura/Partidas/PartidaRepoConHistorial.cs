using System.Collections.Concurrent;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Dominio.Juego;

namespace Infraestructura.Partidas;

/// <summary>
/// Decorador de IPartidaRepo (patrón Decorator): después de cada operación sobre una partida revisa si
/// quedó Finalizada y, en ese caso, la guarda en el historial. Como TODA acción y TODO tick pasan por
/// UsarAsync, esto cubre las dos formas de terminar (reloj y puntos) en un solo lugar, sin tocar el
/// reloj ni el adaptador de tiempo real. Un fallo al guardar nunca rompe la partida: se avisa con
/// <c>alFallar</c> (el log) y se reintenta en la siguiente operación.
/// </summary>
public sealed class PartidaRepoConHistorial(
    IPartidaRepo interno, RegistrarResultadoPartida registrador, Action<Guid, Exception>? alFallar = null) : IPartidaRepo
{
    private readonly ConcurrentDictionary<Guid, byte> _registradas = new();

    public Task AgregarAsync(Partida partida, CancellationToken ct = default) => interno.AgregarAsync(partida, ct);

    public Task<Guid?> BuscarPartidaDeJugadorAsync(Guid usuarioId, CancellationToken ct = default) =>
        interno.BuscarPartidaDeJugadorAsync(usuarioId, ct);

    public Task<IReadOnlyList<TResultado>> ConsultarAsync<TResultado>(
        Func<Partida, bool> filtro, Func<Partida, TResultado> proyeccion, CancellationToken ct = default) =>
        interno.ConsultarAsync(filtro, proyeccion, ct);

    public async Task<TResultado> UsarAsync<TResultado>(
        Guid partidaId, Func<Partida, TResultado> operacion, CancellationToken ct = default)
    {
        // La foto se toma DENTRO del acceso exclusivo; el guardado se hace fuera, sin retener el candado.
        PartidaVista? terminada = null;
        var resultado = await interno.UsarAsync(partidaId, partida =>
        {
            var r = operacion(partida);
            if (partida.Estado == EstadoPartida.Finalizada && !_registradas.ContainsKey(partida.Id))
                terminada = PartidaVista.Desde(partida);
            return r;
        }, ct);

        if (terminada is not null)
        {
            try
            {
                await registrador.RegistrarAsync(terminada, ct);
                _registradas.TryAdd(terminada.Id, 0);
            }
            catch (Exception ex)
            {
                alFallar?.Invoke(terminada.Id, ex);
            }
        }

        return resultado;
    }
}
