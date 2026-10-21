using Aplicacion.Puertos;
using Dominio.Comun;

namespace Aplicacion.CasosDeUso;

public sealed record IniciarPartidaComando(Guid PartidaId, Guid UsuarioId);

/// <summary>Caso de uso: el anfitrión (el primer jugador que entró) inicia la partida.</summary>
public sealed class IniciarPartida(IPartidaRepo partidas)
{
    public Task<PartidaVista> EjecutarAsync(IniciarPartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return partidas.UsarAsync(comando.PartidaId, partida =>
        {
            var anfitrion = partida.Jugadores.FirstOrDefault();
            if (anfitrion is null || anfitrion.Id != comando.UsuarioId)
                throw new DominioException("Solo el anfitrión puede iniciar la partida.");

            partida.Iniciar();
            return PartidaVista.Desde(partida);
        }, ct);
    }
}
