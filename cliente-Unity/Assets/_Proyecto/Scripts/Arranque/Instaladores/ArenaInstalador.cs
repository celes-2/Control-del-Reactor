using System.Collections;
using Reactor.Cliente.Arena;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Arena: toma los servicios del CompositionRoot y crea el presentador.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// Al terminar la partida guarda el resultado en la sesión y abre la escena Victoria.
    /// </summary>
    [RequireComponent(typeof(ArenaVista))]
    public sealed class ArenaInstalador : MonoBehaviour
    {
        [Tooltip("Solo mientras no exista la Sala de espera.")]
        [SerializeField] string partidaIdDemo = "partida-demo";

        [Tooltip("Segundos que se ve el aviso \"¡Ganó ...!\" en la arena antes de abrir la pantalla de victoria.")]
        [SerializeField] float segundosAntesDeVictoria = 2.5f;

        ArenaPresentador presentador;
        CompositionRoot raiz;

        void Start()
        {
            raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            presentador = new ArenaPresentador(GetComponent<ArenaVista>(), raiz.Socket);
            presentador.PartidaTerminada += AlTerminarPartida;

            var partidaId = string.IsNullOrEmpty(raiz.Sesion.PartidaActualId) ? partidaIdDemo : raiz.Sesion.PartidaActualId;
            presentador.Iniciar(partidaId);
        }

        void AlTerminarPartida(PartidaFinalizadaMsg mensaje)
        {
            raiz.Sesion.UltimoResultado = ResultadoPartida.Crear(mensaje, presentador.Jugadores);
            StartCoroutine(IrAVictoria());
        }

        IEnumerator IrAVictoria()
        {
            yield return new WaitForSeconds(segundosAntesDeVictoria);
            SceneManager.LoadScene(Escenas.Victoria);
        }

        void OnDestroy()
        {
            if (presentador == null) return;
            presentador.PartidaTerminada -= AlTerminarPartida;
            presentador.Dispose();
        }
    }
}
