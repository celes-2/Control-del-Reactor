using Reactor.Cliente.Perfil;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Perfil: avatar del jugador. "Continuar" abre Configuración; "Cerrar sesión" vuelve al login.
    /// Si no hay sesión iniciada, regresa al login. Si se da Play directamente en esta escena, pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(PerfilVista))]
    public sealed class PerfilInstalador : MonoBehaviour
    {
        PerfilPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            if (!raiz.Sesion.EstaAutenticado)
            {
                SceneManager.LoadScene(Escenas.Acceso);
                return;
            }

            raiz.Audio.Reproducir(Pista.Menu);

            presentador = new PerfilPresentador(
                GetComponent<PerfilVista>(),
                raiz.Auth,
                raiz.PreparadorImagen,
                raiz.Avatares,
                avatarId => raiz.Sesion.CambiarAvatar(avatarId),
                () => SceneManager.LoadScene(Escenas.Configuracion),
                () =>
                {
                    raiz.Sesion.Cerrar();
                    SceneManager.LoadScene(Escenas.Acceso);
                });
            presentador.Iniciar();
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
