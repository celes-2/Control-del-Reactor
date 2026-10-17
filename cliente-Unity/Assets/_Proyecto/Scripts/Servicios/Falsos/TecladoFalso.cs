using Reactor.Cliente.Contratos;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// Simula dos Pico W con el teclado, SOLO en modo de desarrollo.
    /// Jugador 1: W A S D + Espacio.  Jugador 2: flechas + Enter.
    /// En el juego real los botones llegan al servidor por MQTT, nunca desde Unity.
    /// </summary>
    public sealed class TecladoFalso : MonoBehaviour
    {
        SocketFalso socket;

        public void Iniciar(SocketFalso socketFalso) => socket = socketFalso;

        void Update()
        {
            if (socket == null) return;

#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return;

            if (k.wKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador1, Botones.Arriba);
            if (k.sKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador1, Botones.Abajo);
            if (k.aKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador1, Botones.Izquierda);
            if (k.dKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador1, Botones.Derecha);
            if (k.spaceKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador1, Botones.Accion);

            if (k.upArrowKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador2, Botones.Arriba);
            if (k.downArrowKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador2, Botones.Abajo);
            if (k.leftArrowKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador2, Botones.Izquierda);
            if (k.rightArrowKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador2, Botones.Derecha);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Enviar(SocketFalso.Jugador2, Botones.Accion);
#else
            if (Input.GetKeyDown(KeyCode.W)) Enviar(SocketFalso.Jugador1, Botones.Arriba);
            if (Input.GetKeyDown(KeyCode.S)) Enviar(SocketFalso.Jugador1, Botones.Abajo);
            if (Input.GetKeyDown(KeyCode.A)) Enviar(SocketFalso.Jugador1, Botones.Izquierda);
            if (Input.GetKeyDown(KeyCode.D)) Enviar(SocketFalso.Jugador1, Botones.Derecha);
            if (Input.GetKeyDown(KeyCode.Space)) Enviar(SocketFalso.Jugador1, Botones.Accion);

            if (Input.GetKeyDown(KeyCode.UpArrow)) Enviar(SocketFalso.Jugador2, Botones.Arriba);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Enviar(SocketFalso.Jugador2, Botones.Abajo);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Enviar(SocketFalso.Jugador2, Botones.Izquierda);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Enviar(SocketFalso.Jugador2, Botones.Derecha);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Enviar(SocketFalso.Jugador2, Botones.Accion);
#endif
        }

        void Enviar(string jugadorId, string boton) => socket.SimularBoton(jugadorId, boton);
    }
}
