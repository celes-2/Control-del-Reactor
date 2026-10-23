using Reactor.Cliente.Servicios;
using Reactor.Cliente.Servicios.Falsos;
using Reactor.Cliente.Ui;
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

        [Tooltip("SOLO con el servidor falso: empezar en la pantalla de Acceso (login con cuentas falsas) en vez de entrar directo como Ana.")]
        [SerializeField] bool iniciarEnAcceso;

        [Tooltip("Escena que se abre después del arranque si ya hay sesión. Con el servidor falso: Arena (directo a jugar) o SalaEspera (flujo completo).")]
        [SerializeField] string escenaInicial = Escenas.Configuracion;

        public ConfigCliente Config { get; private set; }
        public SessionStore Sesion { get; private set; }
        public IGameSocket Socket { get; private set; }
        public IApiPartidas Api { get; private set; }
        public IApiAuth Auth { get; private set; }
        public IAvatarCache Avatares { get; private set; }
        public IPreparadorImagen PreparadorImagen { get; private set; }

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
                if (!iniciarEnAcceso)
                    Sesion.Iniciar("token-falso", SocketFalso.Jugador1, "Ana"); // en modo falso, el jugador local es Ana (anfitriona)
                Socket = falso;
                Api = new ApiPartidasFalsa();
                Auth = new ApiAuthFalsa(SocketFalso.Jugador1);
                Avatares = new AvatarCacheFalsa();
                PreparadorImagen = new PreparadorImagenFalso();
                gameObject.AddComponent<TecladoFalso>().Iniciar(falso);
                Debug.Log("Modo desarrollo: servidor falso. Jugador 1: WASD + Espacio. Jugador 2: flechas + Enter.");
            }
            else
            {
                // Con "usuarioId" en config.json se entra sin login (solo desarrollo). Sin él se pasa por la pantalla Acceso,
                // y Sesion.Iniciar guarda el token del login.
                if (!Sesion.EstaAutenticado && !string.IsNullOrEmpty(Config.UsuarioIdProvisional))
                    Sesion.IniciarProvisional(Config.UsuarioIdProvisional, Config.NombreProvisional);

                Socket = new GameSocket(Config.UrlWebSocket, Sesion);
                Api = new ApiPartidas(Config.Servidor, Sesion);
                Auth = new ApiAuth(Config.Servidor, Sesion);
                Avatares = new AvatarCache(Config.Servidor);
                PreparadorImagen = new PreparadorImagenAvatar();
            }

            // Barra con el avatar y el nombre del jugador en todas las pantallas (CA-06.5).
            gameObject.AddComponent<BarraUsuario>().Iniciar(Sesion, Auth, Avatares);

            // Sin sesión (ni token ni usuario provisional) se empieza en el login.
            var tieneSesion = Sesion.EstaAutenticado || !string.IsNullOrEmpty(Sesion.UsuarioId);
            SceneManager.LoadScene(tieneSesion ? escenaInicial : Escenas.Acceso);
        }

        void Update() => Socket?.Despachar();

        void OnApplicationQuit() => Socket?.Cerrar();
    }
}
