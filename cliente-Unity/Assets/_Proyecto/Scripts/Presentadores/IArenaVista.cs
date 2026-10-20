using System.Collections.Generic;
using Reactor.Cliente.Contratos;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lo que el presentador puede pedirle a la escena Arena (patrón MVP).
    /// La vista solo dibuja: no tiene reglas ni estado de juego propio.
    /// </summary>
    public interface IArenaVista
    {
        void DibujarEscenario(EscenarioMsg escenario);
        void LimpiarJugadores();
        void MostrarJugador(JugadorMsg jugador);
        void MoverJugador(string jugadorId, int x, int y);
        void MostrarAccion(string jugadorId, string mensaje);
        void ActualizarMarcador(IEnumerable<JugadorMsg> jugadores);
        void ActualizarTemporizador(int segundos, bool esCuentaRegresiva);
        void MostrarEstado(string texto);
        void MostrarAviso(string texto);
    }
}
