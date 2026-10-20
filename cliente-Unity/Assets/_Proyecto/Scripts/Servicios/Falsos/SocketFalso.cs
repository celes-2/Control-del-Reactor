using System;
using System.Collections.Generic;
using System.Linq;
using Reactor.Cliente.Contratos;
using UnityEngine;

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// Imita al servidor autoritativo para desarrollar la arena sin backend ni hardware.
    /// Aplica las mismas reglas que el dominio: bordes, muros, reactor y otros jugadores
    /// bloquean; cada movimiento válido suma 1 punto; en empate se suman 2 minutos.
    /// SOLO para desarrollo: en la versión real esto lo decide el servidor.
    /// </summary>
    public sealed class SocketFalso : IGameSocket
    {
        public const string Jugador1 = "jugador-1";
        public const string Jugador2 = "jugador-2";

        const int MinutosPartida = 1;
        const int MinutosExtraPorEmpate = 2;

        // Mismo formato que scenarios.json: W muro, . piso, S inicio, R reactor.
        static readonly string[] Mapa =
        {
            "WWWWWWWWWWWWWWW",
            "WS...........SW",
            "W..WW.....WW..W",
            "W..W.......W..W",
            "W......W......W",
            "W.....WRW.....W",
            "W......W......W",
            "W..W.......W..W",
            "W..WW.....WW..W",
            "WS...........SW",
            "WWWWWWWWWWWWWWW",
        };

        const float SegundosParaQueEntreBeto = 3f;

        readonly Dictionary<string, JugadorMsg> jugadores = new Dictionary<string, JugadorMsg>();
        EstadoPartidaMsg estado;
        float acumulado;
        JugadorMsg betoEnEspera;
        float esperaAcumulada;

        /// <summary>
        /// Activado: la partida empieza en Espera con Ana sola, Beto entra a los pocos segundos y la partida
        /// arranca cuando la anfitriona envía match:start. Desactivado: empieza directo en curso con los dos.
        /// </summary>
        public bool EmpezarEnSalaDeEspera { get; set; }

        public event Action Conectado;
        public event Action<EstadoPartidaMsg> EstadoRecibido;
        public event Action<JugadorMovidoMsg> JugadorMovido;
        public event Action<JugadorActuoMsg> JugadorActuo;
        public event Action<PuntajeMsg> PuntajeCambiado;
        public event Action<TickMsg> Tick;
        public event Action<TiempoExtraMsg> TiempoExtra;
        public event Action<PartidaFinalizadaMsg> PartidaFinalizada;
        public event Action<string> Error;

        public void Conectar(string partidaId)
        {
            // Idempotente: si ya hay una partida viva con ese id (por ejemplo al pasar de la sala de espera
            // a la arena), solo se reenvía su estado actual.
            if (estado != null && estado.PartidaId == partidaId && estado.Estado != EstadosPartida.Finalizada)
            {
                Conectado?.Invoke();
                EmitirEstado();
                return;
            }

            var inicios = BuscarInicios();
            if (inicios.Count < 2)
            {
                Error?.Invoke("El mapa de prueba necesita al menos dos puntos de inicio.");
                return;
            }

            jugadores.Clear();
            jugadores[Jugador1] = new JugadorMsg { Id = Jugador1, Nombre = "Ana", Color = "#E74C3C", X = inicios[0].x, Y = inicios[0].y };
            var beto = new JugadorMsg { Id = Jugador2, Nombre = "Beto", Color = "#3498DB", X = inicios[1].x, Y = inicios[1].y };
            if (EmpezarEnSalaDeEspera) betoEnEspera = beto;
            else
            {
                betoEnEspera = null;
                jugadores[Jugador2] = beto;
            }

            estado = new EstadoPartidaMsg
            {
                PartidaId = partidaId,
                Estado = EmpezarEnSalaDeEspera ? EstadosPartida.Espera : EstadosPartida.EnCurso,
                AnfitrionId = Jugador1,
                ModoFin = ModosFin.Tiempo,
                ValorFin = MinutosPartida,
                SegundosRestantes = MinutosPartida * 60,
                SegundosTranscurridos = 0,
                Escenario = new EscenarioMsg { Ancho = Mapa[0].Length, Alto = Mapa.Length, Filas = Mapa.ToList() },
            };
            acumulado = 0f;
            esperaAcumulada = 0f;

            Conectado?.Invoke();
            EmitirEstado();
        }

        /// <summary>Equivale a que llegue un evento MQTT de la Pico W del jugador.</summary>
        public void SimularBoton(string jugadorId, string boton)
        {
            if (estado == null || estado.Estado != EstadosPartida.EnCurso) return; // sin partida en curso no hay acciones

            if (!jugadores.TryGetValue(jugadorId, out var jugador))
            {
                Error?.Invoke("Dispositivo no asociado a ningún jugador.");
                return;
            }

            if (boton == Botones.Accion)
            {
                JugadorActuo?.Invoke(new JugadorActuoMsg { JugadorId = jugadorId, Mensaje = "¡Acción!" });
                return;
            }

            var (dx, dy) = Delta(boton);
            if (dx == 0 && dy == 0) return;

            int nx = jugador.X + dx;
            int ny = jugador.Y + dy;
            if (!EsTransitable(nx, ny) || EstaOcupada(nx, ny, jugadorId)) return; // bloqueado: el servidor no difunde nada

            jugador.X = nx;
            jugador.Y = ny;
            jugador.Puntaje++;

            JugadorMovido?.Invoke(new JugadorMovidoMsg { JugadorId = jugadorId, X = nx, Y = ny, Puntaje = jugador.Puntaje });
            PuntajeCambiado?.Invoke(new PuntajeMsg { JugadorId = jugadorId, Puntaje = jugador.Puntaje });

            if (EsModo(ModosFin.Puntos) && jugador.Puntaje >= estado.ValorFin) Finalizar();
        }

        public void Enviar(string tipo, object datos)
        {
            if (tipo == TiposMensaje.Iniciar)
            {
                IniciarPartida();
                return;
            }
            Debug.Log($"[SocketFalso] Mensaje ignorado: {tipo}");
        }

        void IniciarPartida()
        {
            if (estado == null || estado.Estado != EstadosPartida.Espera) return;
            if (jugadores.Count < 2)
            {
                Error?.Invoke("Se necesitan al menos 2 jugadores para iniciar la partida.");
                return;
            }

            estado.Estado = EstadosPartida.EnCurso;
            estado.SegundosRestantes = MinutosPartida * 60;
            estado.SegundosTranscurridos = 0;
            acumulado = 0f;
            EmitirEstado();
        }

        void EmitirEstado()
        {
            estado.Jugadores = jugadores.Values.Select(Copiar).ToList();
            EstadoRecibido?.Invoke(estado);
        }

        public void Despachar()
        {
            if (estado == null) return;

            if (estado.Estado == EstadosPartida.Espera)
            {
                // Simula que otro usuario se une a la sala unos segundos después.
                if (betoEnEspera != null)
                {
                    esperaAcumulada += Time.deltaTime;
                    if (esperaAcumulada >= SegundosParaQueEntreBeto)
                    {
                        jugadores[Jugador2] = betoEnEspera;
                        betoEnEspera = null;
                        EmitirEstado();
                    }
                }
                return;
            }

            if (estado.Estado != EstadosPartida.EnCurso) return;

            acumulado += Time.deltaTime;
            while (acumulado >= 1f && estado.Estado == EstadosPartida.EnCurso)
            {
                acumulado -= 1f;
                estado.SegundosTranscurridos++;
                if (EsModo(ModosFin.Tiempo)) estado.SegundosRestantes = Math.Max(0, estado.SegundosRestantes - 1);

                Tick?.Invoke(new TickMsg { SegundosRestantes = estado.SegundosRestantes, SegundosTranscurridos = estado.SegundosTranscurridos });

                if (EsModo(ModosFin.Tiempo) && estado.SegundosRestantes == 0) Finalizar();
            }
        }

        public void Cerrar()
        {
            estado = null;
        }

        void Finalizar()
        {
            var orden = jugadores.Values.OrderByDescending(j => j.Puntaje).ToList();

            if (orden.Count > 1 && orden[0].Puntaje == orden[1].Puntaje)
            {
                // CA-16.4: empate -> +2 minutos y la partida continúa conservando el puntaje
                estado.SegundosRestantes += MinutosExtraPorEmpate * 60;
                TiempoExtra?.Invoke(new TiempoExtraMsg { MinutosExtra = MinutosExtraPorEmpate, SegundosRestantes = estado.SegundosRestantes });
                return;
            }

            estado.Estado = EstadosPartida.Finalizada;
            PartidaFinalizada?.Invoke(new PartidaFinalizadaMsg
            {
                GanadorId = orden[0].Id,
                Puntajes = orden.Select(j => new PuntajeFinalMsg { JugadorId = j.Id, Nombre = j.Nombre, Puntaje = j.Puntaje }).ToList(),
            });
        }

        bool EsModo(string modo) => string.Equals(estado.ModoFin, modo, StringComparison.OrdinalIgnoreCase);

        static (int dx, int dy) Delta(string boton)
        {
            // Origen (0,0) arriba a la izquierda: "arriba" resta en Y.
            switch (boton)
            {
                case Botones.Arriba: return (0, -1);
                case Botones.Abajo: return (0, 1);
                case Botones.Izquierda: return (-1, 0);
                case Botones.Derecha: return (1, 0);
                default: return (0, 0);
            }
        }

        static bool EsTransitable(int x, int y)
        {
            if (y < 0 || y >= Mapa.Length || x < 0 || x >= Mapa[y].Length) return false;
            var celda = Mapa[y][x];
            return celda == Celdas.Piso || celda == Celdas.Inicio;
        }

        bool EstaOcupada(int x, int y, string excepto) =>
            jugadores.Values.Any(j => j.Id != excepto && j.X == x && j.Y == y);

        static List<Vector2Int> BuscarInicios()
        {
            var inicios = new List<Vector2Int>();
            for (int y = 0; y < Mapa.Length; y++)
                for (int x = 0; x < Mapa[y].Length; x++)
                    if (Mapa[y][x] == Celdas.Inicio) inicios.Add(new Vector2Int(x, y));
            return inicios;
        }

        static JugadorMsg Copiar(JugadorMsg j) => new JugadorMsg
        {
            Id = j.Id, Nombre = j.Nombre, AvatarUrl = j.AvatarUrl, Color = j.Color, X = j.X, Y = j.Y, Puntaje = j.Puntaje,
        };
    }
}
