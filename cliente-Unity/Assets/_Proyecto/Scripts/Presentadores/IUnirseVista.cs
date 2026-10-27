using System;
using System.Collections.Generic;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>Lo que el presentador puede pedirle a la escena Unirse (patrón MVP).</summary>
    public interface IUnirseVista
    {
        /// <summary>El jugador pulsó "Unirse" en la partida con ese id.</summary>
        event Action<string> UnirsePulsado;

        /// <summary>Pidió actualizar la lista (botón o refresco automático).</summary>
        event Action ActualizarPulsado;

        event Action VolverPulsado;

        void MostrarPartidas(IReadOnlyList<PartidaAbierta> partidas);
        void MostrarEstado(string texto);
        void MostrarAviso(string texto);
    }
}
