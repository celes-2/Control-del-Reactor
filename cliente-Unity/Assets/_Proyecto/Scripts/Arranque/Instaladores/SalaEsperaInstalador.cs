using Reactor.Cliente.Presentadores;
using Reactor.Cliente.SalaEspera;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Sala de espera: toma los servicios del CompositionRoot y crea el presentador.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(SalaEsperaVista))]
    public sealed class SalaEsperaInstalador : MonoBehaviour
    {
        [Tooltip("Solo mientras no se pueda crear o unirse a una partida desde la interfaz.")]
        [SerializeField] string partidaIdDemo = "partida-demo";

        SalaEsperaPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            var sesion = raiz.Sesion;
            if (string.IsNullOrEmpty(sesion.PartidaActualId)) sesion.PartidaActualId = partidaIdDemo;

            presentador = new SalaEsperaPresentador(
                GetComponent<SalaEsperaVista>(), raiz.Socket, sesion.UsuarioId, () => SceneManager.LoadScene(Escenas.Arena));
            presentador.Iniciar(sesion.PartidaActualId);
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
