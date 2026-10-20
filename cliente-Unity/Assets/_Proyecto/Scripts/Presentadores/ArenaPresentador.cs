using System;
using System.Collections.Generic;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Traduce los eventos del servidor en órdenes para la vista de la arena.
    /// Es C# puro (no usa UnityEngine), así que se puede probar con una vista falsa.
    /// Cada jugador se identifica por su Id: nunca se mezclan identidades (CA-10.7).
    /// </summary>
    public sealed class ArenaPresentador : IDisposable
    {
        readonly IArenaVista vista;
        readonly IGameSocket socket;
        readonly Dictionary<string, JugadorMsg> jugadores = new Dictionary<string, JugadorMsg>();
        string modoFin = ModosFin.Tiempo;

        /// <summary>Jugadores de la partida actual, con su último estado conocido.</summary>
        public IReadOnlyCollection<JugadorMsg> Jugadores => jugadores.Values;

        /// <summary>Se dispara al terminar la partida (aquí se navegará a la escena Victoria).</summary>
        public event Action<PartidaFinalizadaMsg> PartidaTerminada;

        public ArenaPresentador(IArenaVista vista, IGameSocket socket)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));

            socket.EstadoRecibido += AlRecibirEstado;
            socket.JugadorMovido += AlMoverse;
            socket.JugadorActuo += AlActuar;
            socket.PuntajeCambiado += AlCambiarPuntaje;
            socket.Tick += AlTick;
            socket.TiempoExtra += AlTiempoExtra;
            socket.PartidaFinalizada += AlFinalizar;
            socket.Error += AlError;
        }

        public void Iniciar(string partidaId)
        {
            if (string.IsNullOrWhiteSpace(partidaId))
            {
                vista.MostrarAviso("No hay una partida seleccionada.");
                return;
            }
            vista.MostrarEstado("Conectando...");
            socket.Conectar(partidaId);
        }

        public void Dispose()
        {
            // Sin esto, al cambiar de escena los eventos llamarían a objetos destruidos.
            socket.EstadoRecibido -= AlRecibirEstado;
            socket.JugadorMovido -= AlMoverse;
            socket.JugadorActuo -= AlActuar;
            socket.PuntajeCambiado -= AlCambiarPuntaje;
            socket.Tick -= AlTick;
            socket.TiempoExtra -= AlTiempoExtra;
            socket.PartidaFinalizada -= AlFinalizar;
            socket.Error -= AlError;
        }

        void AlRecibirEstado(EstadoPartidaMsg e)
        {
            if (e?.Escenario == null)
            {
                vista.MostrarAviso("El servidor envió una partida sin escenario.");
                return;
            }

            e.AsignarColoresFaltantes();
            modoFin = string.IsNullOrEmpty(e.ModoFin) ? ModosFin.Tiempo : e.ModoFin;
            vista.DibujarEscenario(e.Escenario);

            vista.LimpiarJugadores();
            jugadores.Clear();
            foreach (var j in e.Jugadores ?? new List<JugadorMsg>())
            {
                if (string.IsNullOrEmpty(j?.Id)) continue;
                jugadores[j.Id] = j;
                vista.MostrarJugador(j);
            }

            vista.MostrarEstado(TextoEstado(e.Estado, jugadores.Count));
            ActualizarTemporizador(e.SegundosRestantes, e.SegundosTranscurridos);
            vista.ActualizarMarcador(jugadores.Values);
        }

        void AlMoverse(JugadorMovidoMsg m)
        {
            if (m == null || !jugadores.TryGetValue(m.JugadorId ?? "", out var j)) return;
            j.X = m.X;
            j.Y = m.Y;
            j.Puntaje = m.Puntaje;
            vista.MoverJugador(m.JugadorId, m.X, m.Y);
            vista.ActualizarMarcador(jugadores.Values);
        }

        void AlActuar(JugadorActuoMsg m)
        {
            if (m == null || !jugadores.ContainsKey(m.JugadorId ?? "")) return;
            vista.MostrarAccion(m.JugadorId, string.IsNullOrWhiteSpace(m.Mensaje) ? "¡Acción!" : m.Mensaje);
        }

        void AlCambiarPuntaje(PuntajeMsg m)
        {
            if (m == null || !jugadores.TryGetValue(m.JugadorId ?? "", out var j)) return;
            j.Puntaje = m.Puntaje;
            vista.ActualizarMarcador(jugadores.Values);
        }

        void AlTick(TickMsg m)
        {
            if (m != null) ActualizarTemporizador(m.SegundosRestantes, m.SegundosTranscurridos);
        }

        void AlTiempoExtra(TiempoExtraMsg m)
        {
            if (m == null) return;
            vista.MostrarAviso($"¡Empate! +{m.MinutosExtra} minutos");
            if (EsModoTiempo()) vista.ActualizarTemporizador(m.SegundosRestantes, true);
        }

        void AlFinalizar(PartidaFinalizadaMsg m)
        {
            if (m == null) return;
            var nombre = jugadores.TryGetValue(m.GanadorId ?? "", out var g) ? g.Nombre : "un jugador";
            vista.MostrarEstado(TextoEstado(EstadosPartida.Finalizada, jugadores.Count));
            vista.MostrarAviso($"¡Ganó {nombre}!");
            PartidaTerminada?.Invoke(m);
        }

        void AlError(string mensaje) => vista.MostrarAviso(mensaje);

        void ActualizarTemporizador(int restantes, int transcurridos)
        {
            // CA-09.9: el temporizador se muestra siempre; en modo puntos cuenta hacia arriba.
            if (EsModoTiempo()) vista.ActualizarTemporizador(restantes, true);
            else vista.ActualizarTemporizador(transcurridos, false);
        }

        bool EsModoTiempo() => string.Equals(modoFin, ModosFin.Tiempo, StringComparison.OrdinalIgnoreCase);

        static string TextoEstado(string estado, int cantidadJugadores)
        {
            switch (estado)
            {
                case EstadosPartida.Espera: return cantidadJugadores < 2 ? "Esperando jugador..." : "Esperando inicio";
                case EstadosPartida.EnCurso: return "Partida en curso";
                case EstadosPartida.Finalizada: return "Partida finalizada";
                default: return estado ?? "";
            }
        }
    }
}
