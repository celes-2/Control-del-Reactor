using Aplicacion.Puertos;

namespace Aplicacion.CasosDeUso;

public sealed record AbandonarPartidaComando(Guid PartidaId, Guid UsuarioId);

public enum ResultadoAbandono
{
    /// <summary>El jugador salió y la partida sigue abierta para los demás.</summary>
    SigueAbierta,

    /// <summary>Salió el anfitrión (o no quedó nadie): la partida se eliminó.</summary>
    Cerrada,
}

/// <summary>
/// Caso de uso: un jugador sale de la sala de espera (botón "Volver al perfil").
/// Si el que sale es el anfitrión, o la sala queda vacía, la partida se elimina y deja de aparecer en la lista.
/// Solo vale antes de que la partida comience.
/// </summary>
public sealed class AbandonarPartida(IPartidaRepo partidas)
{
    public async Task<ResultadoAbandono> EjecutarAsync(AbandonarPartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var cerrar = await partidas.UsarAsync(comando.PartidaId, partida =>
        {
            var esAnfitrion = partida.Jugadores.FirstOrDefault()?.Id == comando.UsuarioId;
            partida.Salir(comando.UsuarioId);   // las reglas (ya comenzó, no estaba) las aplica el dominio
            return esAnfitrion || partida.Jugadores.Count == 0;
        }, ct);

        if (!cerrar)
            return ResultadoAbandono.SigueAbierta;

        await partidas.EliminarAsync(comando.PartidaId, ct);
        return ResultadoAbandono.Cerrada;
    }
}
