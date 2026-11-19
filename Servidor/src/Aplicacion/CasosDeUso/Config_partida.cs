using Aplicacion.Puertos;
using Dominio.Comun;
using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

public sealed record CrearPartidaComando(
    Guid UsuarioId,
    ConfigPartida Config,
    IReadOnlyList<Obstaculo>? Obstaculos = null);

/// <summary>
/// Caso de uso: crear y configurar una partida nueva. Quien la crea queda como anfitrión
/// y entra automáticamente. La configuración se valida en el dominio.
/// Si no se envían obstáculos se usa un escenario del catálogo (US-23): el de Config.EscenarioId
/// o, si no se eligió ninguno, el mapa clásico 15x11. El tamaño y las trampas aleatorias se toman del escenario.
/// Si se envían obstáculos, se arma una arena en blanco con el tamaño de la configuración
/// (no se puede pedir escenario y obstáculos a la vez).
/// </summary>
public sealed class ConfigurarPartida(IUsuarioRepo usuarios, IPartidaRepo partidas)
{
    public async Task<PartidaVista> EjecutarAsync(CrearPartidaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(comando.Config);

        var anfitrion = await usuarios.ObtenerJugadorHabilitadoAsync(comando.UsuarioId, ct);

        var partida = comando.Obstaculos is null
            ? CrearSobreEscenario(comando.Config)
            : CrearConObstaculos(comando.Config, comando.Obstaculos);
        partida.Unirse(anfitrion.Id, anfitrion.Nombre, anfitrion.AvatarId);

        await partidas.AgregarAsync(partida, ct);
        return PartidaVista.Desde(partida);
    }

    /// <summary>Carga el escenario elegido antes de que empiece la partida (CA-23.3). Un id desconocido falla.</summary>
    private static Partida CrearSobreEscenario(ConfigPartida config)
    {
        var escenario = Escenarios.Obtener(config.EscenarioId);

        // El escenario pone su id tal como está en el catálogo ("Laboratorio" -> "laboratorio"; el clásico queda null)
        // y cuántas trampas se reparten al azar (viene en su JSON).
        return Partida.Crear(escenario.Aplicar(config), escenario.CrearArena());
    }

    private static Partida CrearConObstaculos(ConfigPartida config, IReadOnlyList<Obstaculo> obstaculos)
    {
        if (!string.IsNullOrWhiteSpace(config.EscenarioId))
            throw new DominioException("Elija un escenario o envíe obstáculos propios, no las dos cosas.");

        return Partida.Crear(config, obstaculos);
    }
}
