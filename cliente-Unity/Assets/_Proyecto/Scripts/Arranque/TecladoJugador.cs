using Reactor.Cliente.Contratos;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Mueve al jugador local con el teclado (servidor real), a la vez que el control físico.
    /// WASD o flechas para moverse; Espacio o Enter para extraer. Solo actúa dentro de la arena.
    /// No decide nada: manda "player:input" y el servidor aplica las mismas reglas que con la Pico.
    /// </summary>
    public sealed class TecladoJugador : MonoBehaviour
    {
        IGameSocket socket;

        public void Iniciar(IGameSocket gameSocket) => socket = gameSocket;

        void Update()
        {
            if (socket == null || SceneManager.GetActiveScene().name != Escenas.Arena) return;

            var boton = LeerBoton();
            if (boton != null) socket.Enviar(TiposMensaje.Entrada, new { button = boton });
        }

        static string LeerBoton()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return null;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) return Botones.Arriba;
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) return Botones.Abajo;
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) return Botones.Izquierda;
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) return Botones.Derecha;
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) return Botones.Accion;
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return Botones.Arriba;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return Botones.Abajo;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return Botones.Izquierda;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return Botones.Derecha;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return Botones.Accion;
#endif
            return null;
        }
    }
}
