using Aplicacion.Puertos;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

public sealed record CrearPartidaComando(
    Guid UsuarioId,
    ConfigPartida Config,
    IReadOnlyList<Obstaculo>? Obstaculos = null);

/// <summary>
/// Caso de uso: crear y configurar una partida nueva. Quien la crea queda como anfitrión
/// y entra automáticamente. La configuración se valida en el dominio.
/// Si no se envían obstáculos se usa el escenario predeterminado (mapa 15x11 con muros, reactor
/// e inicios); entonces el ancho y el alto de la configuración se toman del escenario.
/// Si se envían obstáculos, se arma una arena en blanco con el tamaño de la configuración.
/// </summary>
public sealed class ConfigurarPartida(IUsuarioRepo usuarios, IPartidaRepo partidas)
{
    public async Task<PartidaVista> EjecutarAsync(CrearPartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var anfitrion = await usuarios.ObtenerJugadorHabilitadoAsync(comando.UsuarioId, ct);

        var partida = comando.Obstaculos is null
            ? Partida.Crear(comando.Config, Arena.DesdeFilas(Escenarios.Predeterminado))
            : Partida.Crear(comando.Config, comando.Obstaculos);
        partida.Unirse(anfitrion.Id, anfitrion.Nombre, anfitrion.AvatarId);

        await partidas.AgregarAsync(partida, ct);
        return PartidaVista.Desde(partida);
    }
}
