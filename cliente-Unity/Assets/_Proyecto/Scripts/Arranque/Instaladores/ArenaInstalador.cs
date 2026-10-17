using Reactor.Cliente.Arena;
using Reactor.Cliente.Presentadores;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Arena: toma los servicios del CompositionRoot y crea el presentador.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(ArenaVista))]
    public sealed class ArenaInstalador : MonoBehaviour
    {
        [Tooltip("Solo mientras no exista la Sala de espera.")]
        [SerializeField] string partidaIdDemo = "partida-demo";

        ArenaPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            presentador = new ArenaPresentador(GetComponent<ArenaVista>(), raiz.Socket);

            var partidaId = string.IsNullOrEmpty(raiz.Sesion.PartidaActualId) ? partidaIdDemo : raiz.Sesion.PartidaActualId;
            presentador.Iniciar(partidaId);
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
