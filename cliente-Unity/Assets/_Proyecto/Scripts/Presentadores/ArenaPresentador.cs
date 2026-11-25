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
        int capacidadReactor;
        int energiaReactor;
        string estadoReactor = EstadosReactor.Disponible;
        bool sobrecargaActiva;   // el servidor avisa la sobrecarga con event:started / event:ended, no con match:state
        readonly string jugadorPropioId;
        readonly Dictionary<string, int> extracciones = new Dictionary<string, int>();
        readonly HashSet<string> congelados = new HashSet<string>();

        /// <summary>Segundos jugados, según el último tick o estado recibido.</summary>
        public int SegundosTranscurridos { get; private set; }

        /// <summary>Extracciones válidas de un jugador en esta partida (para la ceremonia).</summary>
        public int ExtraccionesDe(string jugadorId) => extracciones.TryGetValue(jugadorId ?? "", out var n) ? n : 0;

        /// <summary>Jugadores de la partida actual, con su último estado conocido.</summary>
        public IReadOnlyCollection<JugadorMsg> Jugadores => jugadores.Values;

        /// <summary>Se dispara al terminar la partida (aquí se navegará a la escena Victoria).</summary>
        public event Action<PartidaFinalizadaMsg> PartidaTerminada;

        public ArenaPresentador(IArenaVista vista, IGameSocket socket, string jugadorPropioId = null)
        {
            this.jugadorPropioId = jugadorPropioId;
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));

            socket.EstadoRecibido += AlRecibirEstado;
            socket.JugadorMovido += AlMoverse;
            socket.JugadorActuo += AlActuar;
            socket.JugadorExtrajo += AlExtraer;
            socket.JugadorRechazado += AlRechazar;
            socket.PuntajeCambiado += AlCambiarPuntaje;
            socket.Tick += AlTick;
            socket.TiempoExtra += AlTiempoExtra;
            socket.PartidaFinalizada += AlFinalizar;
            socket.ArenaActualizada += AlActualizarArena;
            socket.EventoIniciado += AlIniciarEvento;
            socket.EventoTerminado += AlTerminarEvento;
            socket.JugadorCongelado += AlCongelar;
            socket.ItemRecogido += AlRecogerItem;
            socket.ArmaDisparada += AlDispararArma;
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
            socket.JugadorExtrajo -= AlExtraer;
            socket.JugadorRechazado -= AlRechazar;
            socket.PuntajeCambiado -= AlCambiarPuntaje;
            socket.Tick -= AlTick;
            socket.TiempoExtra -= AlTiempoExtra;
            socket.PartidaFinalizada -= AlFinalizar;
            socket.ArenaActualizada -= AlActualizarArena;
            socket.EventoIniciado -= AlIniciarEvento;
            socket.EventoTerminado -= AlTerminarEvento;
            socket.JugadorCongelado -= AlCongelar;
            socket.ItemRecogido -= AlRecogerItem;
            socket.ArmaDisparada -= AlDispararArma;
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
            SegundosTranscurridos = e.SegundosTranscurridos;
            modoFin = string.IsNullOrEmpty(e.ModoFin) ? ModosFin.Tiempo : e.ModoFin;
            vista.DibujarEscenario(e.Escenario, e.EscenarioId);

            vista.LimpiarJugadores();
            jugadores.Clear();
            foreach (var j in e.Jugadores ?? new List<JugadorMsg>())
            {
                if (string.IsNullOrEmpty(j?.Id)) continue;
                jugadores[j.Id] = j;
                vista.MostrarJugador(j);
                if (!string.IsNullOrEmpty(jugadorPropioId) && j.Id == jugadorPropioId)
                    vista.MarcarJugadorPropio(j.Id);
            }

            vista.MostrarEstado(TextoEstado(e.Estado, jugadores.Count));
            ActualizarTemporizador(e.SegundosRestantes, e.SegundosTranscurridos);
            if (e.Reactor != null)
            {
                capacidadReactor = e.Reactor.Capacidad;
                ActualizarReactor(e.Reactor.Energia, e.Reactor.Estado);
            }
            vista.ActualizarMarcador(jugadores.Values);

            // Arena viva: lo que el servidor dibuja encima del mapa (guardianes, trampas, puertas, energías).
            congelados.Clear();
            foreach (var j in jugadores.Values)
                if (j.SegundosCongelado > 0) MarcarCongelado(j.Id, j.SegundosCongelado);
            if (e.Vivo != null) AplicarArenaViva(e.Vivo);
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

        void AlExtraer(JugadorExtrajoMsg m)
        {
            if (m == null || !jugadores.TryGetValue(m.JugadorId ?? "", out var j)) return;
            j.Puntaje = m.Puntaje;
            vista.ActualizarMarcador(jugadores.Values);
            extracciones[m.JugadorId] = ExtraccionesDe(m.JugadorId) + 1;
            vista.OcultarRechazo(); // una extracción válida borra el "Espera un momento" anterior
            vista.MostrarExtraccion(m.JugadorId);
            if (m.Puntos > 0) vista.MostrarAccion(m.JugadorId, $"+{m.Puntos}");
            ActualizarReactor(m.EnergiaReactor, m.EstadoReactor);
        }

        // El servidor envía player:rejected SOLO al jugador que lo causó, así que no hace falta comparar ids.
        void AlRechazar(JugadorRechazadoMsg m)
        {
            if (m == null) return;
            vista.MostrarRechazo(TextosRechazo.Para(m.Codigo));
        }

        void AlCambiarPuntaje(PuntajeMsg m)
        {
            if (m == null || !jugadores.TryGetValue(m.JugadorId ?? "", out var j)) return;
            j.Puntaje = m.Puntaje;
            vista.ActualizarMarcador(jugadores.Values);
        }

        void AlTick(TickMsg m)
        {
            if (m == null) return;
            SegundosTranscurridos = m.SegundosTranscurridos;
            ActualizarTemporizador(m.SegundosRestantes, m.SegundosTranscurridos);
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

        void AlActualizarArena(ArenaActualizadaMsg m)
        {
            if (m?.Vivo == null) return;
            SegundosTranscurridos = m.SegundosTranscurridos;
            AplicarArenaViva(m.Vivo);
        }

        void AplicarArenaViva(ArenaVivaMsg vivo)
        {
            vista.ActualizarArenaViva(vivo);

            // live.frozen es la lista completa: quien ya no aparece se descongeló.
            var ahora = new HashSet<string>();
            foreach (var c in vivo.Congelados ?? new List<CongeladoMsg>())
            {
                if (string.IsNullOrEmpty(c?.JugadorId)) continue;
                ahora.Add(c.JugadorId);
                MarcarCongelado(c.JugadorId, c.Segundos);
            }
            foreach (var id in new List<string>(congelados))
                if (!ahora.Contains(id)) MarcarCongelado(id, 0);
        }

        void MarcarCongelado(string jugadorId, int segundos)
        {
            if (segundos > 0) congelados.Add(jugadorId);
            else congelados.Remove(jugadorId);
            vista.MostrarCongelado(jugadorId, segundos);
        }

        void AlIniciarEvento(EventoIniciadoMsg m)
        {
            if (m == null || string.IsNullOrEmpty(m.IdEvento)) return;
            vista.MostrarEvento(m.IdEvento, string.IsNullOrWhiteSpace(m.Mensaje) ? m.Tipo : m.Mensaje);

            if (m.Tipo == TiposEvento.Sobrecarga)
            {
                sobrecargaActiva = true;
                MostrarReactorActual();
            }
        }

        void AlTerminarEvento(EventoTerminadoMsg m)
        {
            if (m == null || string.IsNullOrEmpty(m.IdEvento)) return;
            vista.OcultarEvento(m.IdEvento);

            if (m.Tipo == TiposEvento.Sobrecarga)
            {
                sobrecargaActiva = false;
                // Al terminar la sobrecarga el reactor vuelve a su estado normal según la energía que le queda.
                estadoReactor = energiaReactor <= 0 ? EstadosReactor.Vacio : EstadosReactor.Disponible;
                MostrarReactorActual();
            }
        }

        /// <summary>Guarda lo último que dijo el servidor del reactor y lo dibuja (en sobrecarga se ve desactivado).</summary>
        void ActualizarReactor(int energia, string estado)
        {
            energiaReactor = energia;
            estadoReactor = string.IsNullOrEmpty(estado) ? EstadosReactor.Disponible : estado;
            MostrarReactorActual();
        }

        void MostrarReactorActual() =>
            vista.MostrarReactor(energiaReactor, capacidadReactor, sobrecargaActiva ? EstadosReactor.Desactivado : estadoReactor);

        // Arma paralizadora: ACCIÓN lejos del reactor. Los guardianes paralizados se ven en arena:update (stunnedSeconds).
        void AlDispararArma(ArmaDisparadaMsg m)
        {
            if (m == null || !jugadores.ContainsKey(m.JugadorId ?? "")) return;
            var n = m.IdsGuardianes?.Count ?? 0;
            vista.MostrarAccion(m.JugadorId,
                n == 0 ? "¡Disparo! Sin guardianes cerca" : n == 1 ? "¡Guardián paralizado!" : $"¡{n} guardianes paralizados!");
        }

        void AlCongelar(JugadorCongeladoMsg m)
        {
            if (m == null || !jugadores.ContainsKey(m.JugadorId ?? "")) return;
            MarcarCongelado(m.JugadorId, m.Segundos);
            vista.MostrarAccion(m.JugadorId, m.Origen == "GUARDIAN" ? "¡Guardián!" : "¡Trampa!");
        }

        // El puntaje de la energía recogida llega aparte, en score:changed.
        void AlRecogerItem(ItemRecogidoMsg m)
        {
            if (m == null || !jugadores.ContainsKey(m.JugadorId ?? "")) return;
            vista.MostrarAccion(m.JugadorId, $"+{m.Puntos}");
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
