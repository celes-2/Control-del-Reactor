using Reactor.Cliente.Inicio;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Inicio (US-04), la primera pantalla después de iniciar sesión: perfil, historial, ranking y logros.
    /// "Elegir avatar" abre la escena Perfil; "Configuración" abre la creación de partida; "Cerrar sesión" vuelve al login.
    /// Si no hay sesión iniciada, regresa al login. Si se da Play directamente en esta escena, pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(InicioVista))]
    public sealed class InicioInstalador : MonoBehaviour
    {
        InicioPresentador presentador;

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

            presentador = new InicioPresentador(
                GetComponent<InicioVista>(),
                raiz.PerfilApi,
                raiz.Avatares,
                raiz.Sesion.UsuarioId,
                avatarId => raiz.Sesion.CambiarAvatar(avatarId),
                () => SceneManager.LoadScene(Escenas.Perfil),
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
