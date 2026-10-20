using System;
using System.Collections.Generic;
using Reactor.Cliente.Contratos;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lo que el presentador puede pedirle a la escena Sala de espera (patrón MVP).
    /// La vista solo dibuja: no decide quién es anfitrión ni cuándo se puede iniciar.
    /// </summary>
    public interface ISalaEsperaVista
    {
        /// <summary>El anfitrión pulsó "Iniciar partida".</summary>
        event Action IniciarPulsado;

        void MostrarCodigoPartida(string partidaId);
        void MostrarEstado(string texto);
        void MostrarJugadores(IReadOnlyList<JugadorMsg> jugadores, string anfitrionId, string usuarioId);
        void ConfigurarBotonIniciar(bool visible, bool habilitado);
        void MostrarAviso(string texto);
    }
}
