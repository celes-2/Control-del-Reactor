using Reactor.Cliente.Acceso;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Acceso: toma los servicios del CompositionRoot y crea el presentador.
    /// Al iniciar sesión guarda el token en la sesión y abre la escena Perfil (elegir avatar).
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(AccesoVista))]
    public sealed class AccesoInstalador : MonoBehaviour
    {
        AccesoPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            raiz.Audio.Reproducir(Pista.Menu);

            presentador = new AccesoPresentador(
                GetComponent<AccesoVista>(),
                raiz.Auth,
                sesion =>
                {
                    raiz.Sesion.Iniciar(sesion.Token, sesion.UsuarioId, sesion.Nombre);
                    raiz.Sesion.CambiarAvatar(sesion.AvatarId);
                    SceneManager.LoadScene(Escenas.Perfil);
                });
            presentador.Iniciar();
        }

        void OnDestroy() => presentador?.Dispose();
    }
}
