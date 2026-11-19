using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// API de partidas para desarrollo sin servidor: siempre "crea" la partida demo y lista una partida abierta.
    /// Los escenarios salen de los archivos de StreamingAssets/Escenarios (editables), y el escenario y la cantidad de
    /// guardianes que se elijan en Configuración los usa el SocketFalso al jugar.
    /// </summary>
    public sealed class ApiPartidasFalsa : IApiPartidas
    {
        readonly SocketFalso socket;

        public ApiPartidasFalsa(SocketFalso socket = null) => this.socket = socket;

        public string UltimoUsuario { get; private set; }
        public AjustesPartida UltimosAjustes { get; private set; }

        public async Task<string> CrearPartidaAsync(string usuarioId, AjustesPartida ajustes)
        {
            UltimoUsuario = usuarioId;
            UltimosAjustes = ajustes;
            socket?.ConfigurarPartida(ajustes.EscenarioId, ajustes.CantidadGuardianes);
            await Task.Yield();
            return "partida-demo";
        }

        /// <summary>Los escenarios de los archivos: lo que se ve en la vista previa es lo que se juega.</summary>
        public async Task<IReadOnlyList<EscenarioInfo>> ListarEscenariosAsync()
        {
            await Task.Yield();
            return EscenarioArchivo.CargarTodos().Select(e => e.AInfo()).ToList();
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
