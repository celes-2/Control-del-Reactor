using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Unirse;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Unirse: toma los servicios del CompositionRoot y crea el presentador.
    /// Al elegir una partida guarda su id en la sesión y abre la sala de espera.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(UnirseVista))]
    public sealed class UnirseInstalador : MonoBehaviour
    {
        UnirsePresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            raiz.Audio.Reproducir(Pista.Menu);

            presentador = new UnirsePresentador(
                GetComponent<UnirseVista>(),
                raiz.Api,
                partidaId =>
                {
                    raiz.Sesion.PartidaActualId = partidaId;
                    SceneManager.LoadScene(Escenas.SalaEspera);
                },
                () => SceneManager.LoadScene(Escenas.Configuracion));
            presentador.Iniciar();
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
