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
        [SerializeField] string escenaInicial = Escenas.Configuracion;

        public ConfigCliente Config { get; private set; }
        public SessionStore Sesion { get; private set; }
        public IGameSocket Socket { get; private set; }
        public IApiPartidas Api { get; private set; }

        /// <summary>Música de fondo. Se agrega como componente a este mismo objeto para asignar las pistas en el Inspector.</summary>
        public AudioService Audio { get; private set; }

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
            Audio = GetComponent<AudioService>();
            if (Audio == null) Audio = gameObject.AddComponent<AudioService>();

            if (usarServidorFalso)
            {
                var falso = new SocketFalso();
                // Si el flujo empieza en la sala de espera, la partida falsa también espera y luego se inicia.
                falso.EmpezarEnSalaDeEspera = escenaInicial == Escenas.SalaEspera || escenaInicial == Escenas.Configuracion;
                Sesion.Iniciar("token-falso", SocketFalso.Jugador1, "Ana"); // en modo falso, el jugador local es Ana (anfitriona)
                Socket = falso;
                Api = new ApiPartidasFalsa();
                gameObject.AddComponent<TecladoFalso>().Iniciar(falso);
                Debug.Log("Modo desarrollo: servidor falso. Jugador 1: WASD + Espacio. Jugador 2: flechas + Enter.");
            }
            else
            {
                // Sin login todavía: el usuario sale de config.json ("usuarioId"). Con login, Sesion.Iniciar lo reemplaza.
                if (!Sesion.EstaAutenticado && !string.IsNullOrEmpty(Config.UsuarioIdProvisional))
                    Sesion.IniciarProvisional(Config.UsuarioIdProvisional, Config.NombreProvisional);

                Socket = new GameSocket(Config.UrlWebSocket, Sesion);
                Api = new ApiPartidas(Config.Servidor, Sesion);
            }

            SceneManager.LoadScene(escenaInicial);
        }

        void Update() => Socket?.Despachar();

        void OnApplicationQuit() => Socket?.Cerrar();
    }
}
