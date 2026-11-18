using Aplicacion.Puertos;
using Dominio.Comun;

namespace Aplicacion.CasosDeUso;

public sealed record IniciarPartidaComando(Guid PartidaId, Guid UsuarioId);

/// <summary>
/// Caso de uso: el anfitrión (el primer jugador que entró) inicia la partida.
/// Al iniciar se activa la arena viva (Paquete B): trampas al azar, guardianes, puertas y eventos.
/// El azar se recibe por <see cref="IFuenteAzar"/> para que las pruebas usen una semilla fija;
/// si no se inyecta, cada partida usa un <see cref="Random"/> nuevo.
/// </summary>
public sealed class IniciarPartida(IPartidaRepo partidas, IFuenteAzar? azar = null)
{
    public Task<PartidaVista> EjecutarAsync(IniciarPartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return partidas.UsarAsync(comando.PartidaId, partida =>
        {
            var anfitrion = partida.Jugadores.FirstOrDefault();
            if (anfitrion is null || anfitrion.Id != comando.UsuarioId)
                throw new DominioException("Solo el anfitrión puede iniciar la partida.");

            partida.Iniciar(azar?.Nuevo() ?? new Random());
            partida.TomarNovedades();   // al iniciar no hay nada que avisar aparte de match:state
            return PartidaVista.Desde(partida);
        }, ct);
    }
}
