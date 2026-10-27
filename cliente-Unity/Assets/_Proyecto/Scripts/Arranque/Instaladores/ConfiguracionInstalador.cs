using Reactor.Cliente.Configuracion;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Configuración: toma los servicios del CompositionRoot y crea el presentador.
    /// Al crearse la partida guarda su id en la sesión y abre la sala de espera.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(ConfiguracionVista))]
    public sealed class ConfiguracionInstalador : MonoBehaviour
    {
        ConfiguracionPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            raiz.Audio.Reproducir(Pista.Menu);

            var sesion = raiz.Sesion;
            presentador = new ConfiguracionPresentador(
                GetComponent<ConfiguracionVista>(),
                raiz.Api,
                new AlmacenAjustesPlayerPrefs(),
                sesion.UsuarioId,
                partidaId =>
                {
                    sesion.PartidaActualId = partidaId;
                    SceneManager.LoadScene(Escenas.SalaEspera);
                },
                () => SceneManager.LoadScene(Escenas.Unirse));
            presentador.Iniciar();
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
