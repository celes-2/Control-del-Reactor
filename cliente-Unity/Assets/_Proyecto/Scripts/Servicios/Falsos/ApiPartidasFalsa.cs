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

        /// <summary>Escenarios de prueba para ver el selector sin servidor (no son los reales).</summary>
        public async Task<IReadOnlyList<EscenarioInfo>> ListarEscenariosAsync()
        {
            await Task.Yield();
            return new List<EscenarioInfo>
            {
            new EscenarioInfo
            {
                Id = "laboratorio", Nombre = "Laboratorio", Descripcion = "Pasillos cerrados y puertas.", MaxJugadores = 4,
                Filas = new[]
                {
                    "WWWWWWWWWWWWWWW",
                    "WS...........SW",
                    "W.WW.......WW.W",
                    "W.W....T....W.W",
                    "W....W...W....W",
                    "W..T...R...T..W",
                    "W....W...W....W",
                    "W.W....T....W.W",
                    "W.WW.......WW.W",
                    "W.............W",
                    "WWWWWWWWWWWWWWW",
                },
            },
            new EscenarioInfo
            {
                Id = "estacion", Nombre = "Estación", Descripcion = "Corredores largos.", MaxJugadores = 4,
                Filas = new[]
                {
                    "WWWWWWWWWWWWWWW",
                    "WS..W.....W..SW",
                    "W.W.W.WWW.W.W.W",
                    "W.W...WTW...W.W",
                    "W.WWWWW.WWWWW.W",
                    "WT.....R.....TW",
                    "W.WWWWW.WWWWW.W",
                    "W.W...WTW...W.W",
                    "W.W.W.WWW.W.W.W",
                    "W...W.....W...W",
                    "WWWWWWWWWWWWWWW",
                },
            },
            new EscenarioInfo
            {
                Id = "militar", Nombre = "Base militar", Descripcion = "Terreno abierto con trampas.", MaxJugadores = 4,
                Filas = new[]
                {
                    "WWWWWWWWWWWWWWW",
                    "WS...........SW",
                    "W.............W",
                    "W....T...T....W",
                    "W.............W",
                    "W......R......W",
                    "W.............W",
                    "W....T...T....W",
                    "W.............W",
                    "W.............W",
                    "WWWWWWWWWWWWWWW",
                },
            },
            };
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
