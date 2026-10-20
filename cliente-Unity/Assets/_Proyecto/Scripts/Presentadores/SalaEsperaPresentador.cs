using System;
using System.Collections.Generic;
using System.Linq;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de la sala de espera (US-09): muestra quién está en la partida, "Esperando jugador..." con
    /// menos de 2, y deja iniciar solo al anfitrión cuando hay 2 o más. Cuando el servidor informa que la
    /// partida está en curso, abre la arena. Es C# puro, así que se prueba con una vista falsa.
    /// </summary>
    public sealed class SalaEsperaPresentador : IDisposable
    {
        public const int MinimoJugadores = 2;

        readonly ISalaEsperaVista vista;
        readonly IGameSocket socket;
        readonly string usuarioId;
        readonly Action alEmpezar;

        string anfitrionId;
        int cantidadJugadores;
        bool yaEmpezo;

        public SalaEsperaPresentador(ISalaEsperaVista vista, IGameSocket socket, string usuarioId, Action alEmpezar)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.alEmpezar = alEmpezar ?? throw new ArgumentNullException(nameof(alEmpezar));
            this.usuarioId = usuarioId;

            socket.EstadoRecibido += AlRecibirEstado;
            socket.Error += AlError;
            vista.IniciarPulsado += AlPulsarIniciar;
        }

        public void Iniciar(string partidaId)
        {
            if (string.IsNullOrWhiteSpace(partidaId))
            {
                vista.MostrarAviso("No hay una partida seleccionada.");
                vista.ConfigurarBotonIniciar(false, false);
                return;
            }

            vista.MostrarCodigoPartida(partidaId);
            vista.MostrarEstado("Conectando...");
            vista.ConfigurarBotonIniciar(false, false);
            socket.Conectar(partidaId);
        }

        public void Dispose()
        {
            // Sin esto, al cambiar de escena los eventos llamarían a objetos destruidos.
            socket.EstadoRecibido -= AlRecibirEstado;
            socket.Error -= AlError;
            vista.IniciarPulsado -= AlPulsarIniciar;
        }

        bool EsAnfitrion => !string.IsNullOrEmpty(usuarioId) && usuarioId == anfitrionId;

        void AlRecibirEstado(EstadoPartidaMsg estado)
        {
            if (estado == null || yaEmpezo) return;
            estado.AsignarColoresFaltantes();

            if (estado.Estado == EstadosPartida.EnCurso)
            {
                yaEmpezo = true;
                alEmpezar();
                return;
            }

            if (estado.Estado != EstadosPartida.Espera)
            {
                vista.MostrarAviso("La partida ya terminó.");
                vista.ConfigurarBotonIniciar(false, false);
                return;
            }

            var jugadores = (estado.Jugadores ?? new List<JugadorMsg>()).Where(j => !string.IsNullOrEmpty(j?.Id)).ToList();
            anfitrionId = estado.AnfitrionId;
            cantidadJugadores = jugadores.Count;

            vista.MostrarJugadores(jugadores, anfitrionId, usuarioId);
            vista.MostrarEstado(TextoEstado());
            vista.ConfigurarBotonIniciar(EsAnfitrion, EsAnfitrion && cantidadJugadores >= MinimoJugadores);
        }

        void AlPulsarIniciar()
        {
            if (!EsAnfitrion)
            {
                vista.MostrarAviso("Solo el anfitrión puede iniciar la partida.");
                return;
            }
            if (cantidadJugadores < MinimoJugadores)
            {
                vista.MostrarAviso($"Se necesitan al menos {MinimoJugadores} jugadores para iniciar.");
                return;
            }

            socket.Enviar(TiposMensaje.Iniciar, new object());
        }

        void AlError(string mensaje) => vista.MostrarAviso(mensaje);

        string TextoEstado()
        {
            if (cantidadJugadores < MinimoJugadores) return "Esperando jugador...";
            return EsAnfitrion ? "Listos para empezar" : "Esperando a que el anfitrión inicie";
        }
    }
}
