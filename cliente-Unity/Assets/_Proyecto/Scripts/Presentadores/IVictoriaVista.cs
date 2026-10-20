using System;
using System.Collections.Generic;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lo que el presentador puede pedirle a la escena Victoria (patrón MVP).
    /// La vista solo dibuja: no decide quién ganó.
    /// </summary>
    public interface IVictoriaVista
    {
        /// <summary>El jugador pulsó "Volver".</summary>
        event Action VolverPulsado;

        void MostrarGanador(JugadorResultado ganador, string titulo, string subtitulo);
        void MostrarSinGanador(string titulo, string subtitulo);
        void MostrarPuntajes(IReadOnlyList<JugadorResultado> jugadores);

        /// <summary>Confeti y música de victoria.</summary>
        void Celebrar();

        void MostrarSinResultado(string texto);
    }
}
