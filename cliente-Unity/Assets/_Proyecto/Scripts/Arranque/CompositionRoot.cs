using Reactor.Cliente.Servicios;
using Reactor.Cliente.Servicios.Falsos;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Raíz de composición: el ÚNICO lugar que conoce las clases concretas.
    /// Crea los servicios una vez, sobrevive a los cambios de escena y los entrega
    /// a los instaladores de cada escena. Sin singletons estáticos.
    /// </summary>
    public sealed class CompositionRoot : MonoBehaviour
    {
        [Tooltip("Activado: usa el servidor falso y el teclado. Desactivar para conectarse al servidor real.")]
        [SerializeField] bool usarServidorFalso = true;

        [Tooltip("Escena que se abre después del arranque. Con el servidor falso: Arena (directo a jugar) o SalaEspera (flujo completo). Luego será Login.")]
        [SerializeField] string escenaInicial = Escenas.Arena;

        public ConfigCliente Config { get; private set; }
        public SessionStore Sesion { get; private set; }
        public IGameSocket Socket { get; private set; }

        void Awake()
        {
            if (FindObjectsByType<CompositionRoot>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            Config = ConfigCliente.Cargar();
            Sesion = new SessionStore();

            if (usarServidorFalso)
            {
                var falso = new SocketFalso();
                // Si el flujo empieza en la sala de espera, la partida falsa también espera y luego se inicia.
                falso.EmpezarEnSalaDeEspera = escenaInicial == Escenas.SalaEspera;
                Sesion.Iniciar("token-falso", SocketFalso.Jugador1, "Ana"); // en modo falso, el jugador local es Ana (anfitriona)
                Socket = falso;
                gameObject.AddComponent<TecladoFalso>().Iniciar(falso);
                Debug.Log("Modo desarrollo: servidor falso. Jugador 1: WASD + Espacio. Jugador 2: flechas + Enter.");
            }
            else
            {
                Socket = new GameSocket(Config.UrlWebSocket, Sesion);
            }

            SceneManager.LoadScene(escenaInicial);
        }

        void Update() => Socket?.Despachar();

        void OnApplicationQuit() => Socket?.Cerrar();
    }
}
