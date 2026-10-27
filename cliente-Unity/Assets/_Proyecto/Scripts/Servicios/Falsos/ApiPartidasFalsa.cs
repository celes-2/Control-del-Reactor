using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// API de partidas para desarrollo sin servidor: siempre "crea" la partida demo y lista una partida abierta.
    /// Los ajustes no cambian la partida falsa (el SocketFalso tiene sus propias reglas), pero las pantallas
    /// de Configuración y Unirse se pueden probar completas.
    /// </summary>
    public sealed class ApiPartidasFalsa : IApiPartidas
    {
        public string UltimoUsuario { get; private set; }
        public AjustesPartida UltimosAjustes { get; private set; }

        public async Task<string> CrearPartidaAsync(string usuarioId, AjustesPartida ajustes)
        {
            UltimoUsuario = usuarioId;
            UltimosAjustes = ajustes;
            await Task.Yield();
            return "partida-demo";
        }

        public async Task<IReadOnlyList<PartidaAbierta>> ListarAbiertasAsync()
        {
            await Task.Yield();
            return new List<PartidaAbierta>
            {
                new PartidaAbierta { Id = "partida-demo", Anfitrion = "Ana", Jugadores = 1, MaxJugadores = 4 },
            };
        }
    }
}
