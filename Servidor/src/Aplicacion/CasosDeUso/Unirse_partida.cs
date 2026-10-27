using Aplicacion.Puertos;

namespace Aplicacion.CasosDeUso;

public sealed record UnirsePartidaComando(Guid PartidaId, Guid UsuarioId);

/// <summary>Caso de uso: un jugador entra a una partida que todavía no ha comenzado.</summary>
public sealed class UnirsePartida(IUsuarioRepo usuarios, IPartidaRepo partidas)
{
    public async Task<PartidaVista> EjecutarAsync(UnirsePartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var jugador = await usuarios.ObtenerJugadorHabilitadoAsync(comando.UsuarioId, ct);

        // Las reglas (partida llena, avatar repetido, ya comenzó...) las aplica el dominio.
        return await partidas.UsarAsync(comando.PartidaId, partida =>
        {
            partida.Unirse(jugador.Id, jugador.Nombre, jugador.AvatarId);
            return PartidaVista.Desde(partida);
        }, ct);
    }
}
