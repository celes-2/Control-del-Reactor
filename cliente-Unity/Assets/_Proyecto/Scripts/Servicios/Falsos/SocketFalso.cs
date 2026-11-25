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
    /// bloquean; moverse NO da puntos (Sprint 3); ACCIÓN extrae energía si el jugador está junto al reactor
    /// (+1 punto por unidad, 10 por extracción, enfriamiento de 1 s); en empate se suman 2 minutos.
    /// SOLO para desarrollo: en la versión real esto lo decide el servidor.
    /// </summary>
    public sealed class SocketFalso : IGameSocket
    {
        public const string Jugador1 = "jugador-1";
        public const string Jugador2 = "jugador-2";

        const int MinutosPartida = 1;
        const int MinutosExtraPorEmpate = 2;

        // Mismo formato que scenarios.json: W muro, . piso, S inicio, R reactor.
        // Mapa grande (30 x 20) para probar que la cámara sigue al avatar; la lógica no depende del tamaño.
        const int AnchoMapa = 30, AltoMapa = 20; // solo para el mapa de respaldo, si no hay archivos
        

        static string[] MapaRespaldo()
        {
            var celdas = new char[AltoMapa][];
            for (var y = 0; y < AltoMapa; y++)
            {
                celdas[y] = new char[AnchoMapa];
                for (var x = 0; x < AnchoMapa; x++)
                    celdas[y][x] = x == 0 || y == 0 || x == AnchoMapa - 1 || y == AltoMapa - 1 ? 'W' : '.';
            }

            // Columnas y paredes sueltas que dejan libres las celdas de las trampas, la puerta y el reactor.
            foreach (var (px, py, ancho, alto) in new[]
            {
                (8, 7, 3, 2), (19, 3, 3, 2), (8, 14, 3, 2), (19, 14, 3, 2),
                (4, 10, 2, 5), (24, 6, 2, 5), (12, 5, 6, 1), (12, 15, 6, 1),
                (14, 7, 1, 2), (16, 12, 1, 2), (21, 11, 3, 1), (6, 17, 4, 1), (23, 17, 4, 1), (10, 10, 1, 3),
                (2, 13, 1, 4), (27, 12, 1, 4), (14, 2, 1, 2), (7, 2, 3, 1),
            })
                for (var y = py; y < py + alto; y++)
                    for (var x = px; x < px + ancho; x++)
                        celdas[y][x] = 'W';

            celdas[1][1] = 'S';
            celdas[1][AnchoMapa - 2] = 'S';
            celdas[AltoMapa / 2][AnchoMapa / 2] = 'R';
            return celdas.Select(fila => new string(fila)).ToArray();
        }

        const float SegundosParaQueEntreBeto = 3f;

        // Reactor (mismos valores que ConfigPartida del servidor).
        const int CapacidadReactor = 500;
        const int EnergiaPorExtraccion = 10;
        const int RegeneracionPorSegundo = 5;
        const int SegundosEnfriamiento = 1;

        int energiaReactor = CapacidadReactor;
        readonly Dictionary<string, int> ultimaExtraccion = new Dictionary<string, int>();

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
        public event Action<string> ControlVinculado;
#pragma warning disable CS0067 // el servidor real ya no envía player:acted; se conserva por la interfaz
        public event Action<JugadorActuoMsg> JugadorActuo;
#pragma warning restore CS0067
        public event Action<PuntajeMsg> PuntajeCambiado;
        public event Action<TickMsg> Tick;
        public event Action<TiempoExtraMsg> TiempoExtra;
        public event Action<JugadorExtrajoMsg> JugadorExtrajo;
        public event Action<JugadorRechazadoMsg> JugadorRechazado;
        public event Action<PartidaFinalizadaMsg> PartidaFinalizada;
        public event Action<ArenaActualizadaMsg> ArenaActualizada;
        public event Action<EventoIniciadoMsg> EventoIniciado;
        public event Action<EventoTerminadoMsg> EventoTerminado;
        public event Action<JugadorCongeladoMsg> JugadorCongelado;
        public event Action<ItemRecogidoMsg> ItemRecogido;
        public event Action<ArmaDisparadaMsg> ArmaDisparada;   // el servidor falso no simula el arma
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

            CargarEscenario();
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
                Escenario = new EscenarioMsg { Ancho = mapa[0].Length, Alto = mapa.Length, Filas = mapa.ToList() },
                EscenarioId = escenario.Id,
                CantidadGuardianes = CantidadGuardianes,
            };
            acumulado = 0f;
            esperaAcumulada = 0f;
            energiaReactor = CapacidadReactor;
            ultimaExtraccion.Clear();
            ReiniciarArenaViva();

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
                Extraer(jugador);
                return;
            }

            var (dx, dy) = Delta(boton);
            if (dx == 0 && dy == 0) return;

            // Congelado: no se mueve (el servidor real también lo rechaza).
            if (congeladosSim.ContainsKey(jugadorId)) { Rechazar(jugadorId, "JUGADOR_CONGELADO"); return; }

            int nx = jugador.X + dx;
            int ny = jugador.Y + dy;
            if (!PuedePasar(nx, ny) || EstaOcupada(nx, ny, jugadorId)) return; // bloqueado: el servidor no difunde nada

            jugador.X = nx;
            jugador.Y = ny;
            // Moverse ya no da puntos: solo extraer energía del reactor los da. Las trampas restan 5.
            AlPisar(jugador);
        }

        /// <summary>Imita Partida.Extraer del servidor: mismas condiciones, mismos códigos de rechazo.</summary>
        void Extraer(JugadorMsg jugador)
        {
            if (congeladosSim.ContainsKey(jugador.Id)) { Rechazar(jugador.Id, "JUGADOR_CONGELADO"); return; }
            var reactor = BuscarReactor();
            var distancia = Math.Abs(jugador.X - reactor.x) + Math.Abs(jugador.Y - reactor.y);

            if (distancia != 1) { Rechazar(jugador.Id, "LEJOS_DEL_REACTOR"); return; }
            if (energiaReactor <= 0) { Rechazar(jugador.Id, "REACTOR_VACIO"); return; }
            if (ultimaExtraccion.TryGetValue(jugador.Id, out var ultima) &&
                estado.SegundosTranscurridos - ultima < SegundosEnfriamiento)
            {
                Rechazar(jugador.Id, "ENFRIAMIENTO");
                return;
            }

            var energia = Math.Min(EnergiaPorExtraccion, energiaReactor);
            energiaReactor -= energia;
            var puntos = energia * (doblesSim ? 2 : 1); // 1 punto por unidad de energía, x2 en "puntos dobles"
            jugador.Puntaje += puntos;
            ultimaExtraccion[jugador.Id] = estado.SegundosTranscurridos;

            JugadorExtrajo?.Invoke(new JugadorExtrajoMsg
            {
                JugadorId = jugador.Id,
                Puntos = puntos,
                Puntaje = jugador.Puntaje,
                EnergiaJugador = energia,
                EnergiaReactor = energiaReactor,
                EstadoReactor = EstadoDelReactor(),
                IdEvento = "falso:" + estado.SegundosTranscurridos,
            });

            if (EsModo(ModosFin.Puntos) && jugador.Puntaje >= estado.ValorFin) Finalizar();
        }

        void Rechazar(string jugadorId, string codigo) =>
            JugadorRechazado?.Invoke(new JugadorRechazadoMsg
            {
                JugadorId = jugadorId,
                Codigo = codigo,
                Mensaje = TextosRechazo.Para(codigo),
            });

        string EstadoDelReactor() => energiaReactor <= 0 ? EstadosReactor.Vacio : EstadosReactor.Disponible;

        public void Enviar(string tipo, object datos)
        {
            if (tipo == TiposMensaje.Iniciar)
            {
                IniciarPartida();
                return;
            }
            if (tipo == TiposMensaje.Vincular)
            {
                ControlVinculado?.Invoke("pico-falso");   // en desarrollo el "control" se vincula al instante
                return;
            }
            if (tipo == TiposMensaje.CancelarVinculo) return;
            if (tipo == TiposMensaje.Abandonar) return;
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
            foreach (var j in estado.Jugadores)
                j.SegundosCongelado = congeladosSim.TryGetValue(j.Id, out var s) ? s : 0;
            estado.Vivo = estado.Estado == EstadosPartida.Espera ? null : ConstruirVivo(estado.SegundosTranscurridos);
            estado.Reactor = new ReactorMsg { Energia = energiaReactor, Capacidad = CapacidadReactor, Estado = EstadoDelReactor() };
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
                energiaReactor = Math.Min(CapacidadReactor, energiaReactor + RegeneracionPorSegundo);
                if (EsModo(ModosFin.Tiempo)) estado.SegundosRestantes = Math.Max(0, estado.SegundosRestantes - 1);

                Tick?.Invoke(new TickMsg { SegundosRestantes = estado.SegundosRestantes, SegundosTranscurridos = estado.SegundosTranscurridos });
                SimularArenaViva(estado.SegundosTranscurridos);

                if (EsModo(ModosFin.Tiempo) && estado.SegundosRestantes == 0) Finalizar();
            }
        }

        // ---- Arena viva de mentira (para probar sin servidor) ----
        sealed class GuardianSim { public int Id, X, Y, Pausa; }

        const int SegundosCongelado = 3;
        static readonly int[] DuracionTrampa = { 3, 5, 2 }; // tierra movediza, celda emergente, red
        static readonly string[] TiposDeTrampa = { TiposTrampa.TierraMovediza, TiposTrampa.CeldaEmergente, TiposTrampa.Red };

        // Todo lo siguiente sale del archivo del escenario elegido (StreamingAssets/Escenarios).
        EscenarioArchivo escenario;
        string[] mapa = MapaRespaldo();
        List<(int x, int y)> puertasSim = new List<(int x, int y)>();
        List<(int x, int y)> puntosEnergiaSim = new List<(int x, int y)>();

        /// <summary>Escenario ("laboratorio", ...) y guardianes (2 a 4) de la próxima partida; los fija ApiPartidasFalsa.</summary>
        public string EscenarioId { get; private set; }
        public int CantidadGuardianes { get; private set; } = 4;

        public void ConfigurarPartida(string escenarioId, int guardianes)
        {
            EscenarioId = escenarioId;
            CantidadGuardianes = Math.Max(2, Math.Min(4, guardianes));
        }

        /// <summary>Lee el archivo cada vez que se conecta: se puede editar el escenario y volver a jugar sin reiniciar Unity.</summary>
        void CargarEscenario()
        {
            escenario = EscenarioArchivo.Cargar(EscenarioId)
                ?? EscenarioArchivo.DeFilas("respaldo", "Mapa de respaldo", MapaRespaldo());
            mapa = escenario.FilasJuego;
            puertasSim = escenario.Puertas;
            puntosEnergiaSim = escenario.PuntosEnergia;
        }

        readonly List<GuardianSim> guardianesSim = new List<GuardianSim>();
        readonly List<TrampaMsg> trampasSim = new List<TrampaMsg>();
        readonly Dictionary<string, int> congeladosSim = new Dictionary<string, int>();
        ItemEnergiaMsg itemSim;
        bool doblesSim;
        readonly System.Random azar = new System.Random();
        const string IdEventoDemo = "evento-demo";

        void ReiniciarArenaViva()
        {
            guardianesSim.Clear();
            // Las salidas de guardián del archivo, en orden (arriba, izquierda, derecha, abajo, según cómo se dibujó).
            var salidasG = escenario.InicioGuardianes.Count > 0
                ? escenario.InicioGuardianes
                : new List<(int x, int y)> { (1, 1), (AnchoMapa - 2, AltoMapa - 2), (AnchoMapa / 2, 1), (1, AltoMapa / 2) };
            for (var i = 0; i < CantidadGuardianes; i++)
            {
                var (gx, gy) = salidasG[i % salidasG.Count];
                guardianesSim.Add(new GuardianSim { Id = i + 1, X = gx, Y = gy });
            }

            trampasSim.Clear();
            foreach (var (x, y, tipo) in escenario.Trampas)
                if (EsTransitable(x, y)) trampasSim.Add(new TrampaMsg { X = x, Y = y, Tipo = tipo });

            congeladosSim.Clear();
            itemSim = null;
            doblesSim = false;
        }

        bool PuertaCerrada(int x, int y, int segundo)
        {
            for (var i = 0; i < puertasSim.Count; i++)
                if (puertasSim[i].x == x && puertasSim[i].y == y) return !PuertaAbierta(i, segundo);
            return false;
        }

        // Cada puerta cambia cada 6 s (como el servidor), en un segundo distinto cada una.
        static bool PuertaAbierta(int indice, int segundo) => ((segundo + indice * 2) / 6) % 2 == 0;

        bool PuedePasar(int x, int y) => EsTransitable(x, y) && !PuertaCerrada(x, y, estado?.SegundosTranscurridos ?? 0);

        /// <summary>Lo que ocurre cuando un jugador pisa una celda: trampa (-5 y congelado) o energía suelta (+25).</summary>
        void AlPisar(JugadorMsg jugador)
        {
            foreach (var trampa in trampasSim)
            {
                if (trampa.X != jugador.X || trampa.Y != jugador.Y) continue;
                jugador.Puntaje = Math.Max(0, jugador.Puntaje - 5);
                var segundos = DuracionTrampa[Array.IndexOf(TiposDeTrampa, trampa.Tipo)];
                // Mismo orden que el servidor: primero player:moved con el puntaje ya restado, luego player:frozen.
                JugadorMovido?.Invoke(new JugadorMovidoMsg { JugadorId = jugador.Id, X = jugador.X, Y = jugador.Y, Puntaje = jugador.Puntaje });
                Congelar(jugador.Id, segundos, "TRAP", trampa.Tipo);
                return;
            }

            JugadorMovido?.Invoke(new JugadorMovidoMsg { JugadorId = jugador.Id, X = jugador.X, Y = jugador.Y, Puntaje = jugador.Puntaje });

            if (itemSim != null && itemSim.X == jugador.X && itemSim.Y == jugador.Y)
            {
                var puntos = itemSim.Puntos * (doblesSim ? 2 : 1);
                jugador.Puntaje += puntos;
                PuntajeCambiado?.Invoke(new PuntajeMsg { JugadorId = jugador.Id, Puntaje = jugador.Puntaje });
                ItemRecogido?.Invoke(new ItemRecogidoMsg { ItemId = itemSim.Id, JugadorId = jugador.Id, Puntos = puntos });
                itemSim = null;
            }
        }

        void Congelar(string jugadorId, int segundos, string origen, string tipoTrampa)
        {
            congeladosSim[jugadorId] = (congeladosSim.TryGetValue(jugadorId, out var antes) ? antes : 0) + segundos;
            JugadorCongelado?.Invoke(new JugadorCongeladoMsg
            {
                JugadorId = jugadorId, Segundos = congeladosSim[jugadorId], Origen = origen, TipoTrampa = tipoTrampa,
            });
        }

        /// <summary>
        /// Avanza un segundo: los guardianes persiguen al jugador libre más cercano (se congela si lo alcanzan),
        /// las puertas se abren y cierran, aparece una energía cada 12 s y hay puntos dobles entre los segundos 10 y 20.
        /// </summary>
        void SimularArenaViva(int segundo)
        {
            foreach (var id in congeladosSim.Keys.ToList())
            {
                congeladosSim[id]--;
                if (congeladosSim[id] <= 0) congeladosSim.Remove(id);
            }

            foreach (var g in guardianesSim)
            {
                if (g.Pausa > 0) { g.Pausa--; continue; }
                if (segundo % 2 != 0) continue; // el guardián da 1 paso cada 2 s, como en el servidor

                var objetivo = jugadores.Values.Where(j => !congeladosSim.ContainsKey(j.Id))
                    .OrderBy(j => Math.Abs(j.X - g.X) + Math.Abs(j.Y - g.Y)).FirstOrDefault();
                if (objetivo == null) continue;

                // Un paso hacia el objetivo, primero por el eje con más distancia; si hay pared prueba el otro.
                var dx = Math.Sign(objetivo.X - g.X);
                var dy = Math.Sign(objetivo.Y - g.Y);
                var primeroX = Math.Abs(objetivo.X - g.X) >= Math.Abs(objetivo.Y - g.Y);
                var pasos = primeroX ? new[] { (dx, 0), (0, dy), (0, 1), (0, -1) } : new[] { (0, dy), (dx, 0), (1, 0), (-1, 0) };
                foreach (var (px, py) in pasos)
                {
                    if (px == 0 && py == 0) continue;
                    if (!PuedePasar(g.X + px, g.Y + py) || guardianesSim.Any(o => o != g && o.X == g.X + px && o.Y == g.Y + py)) continue;
                    g.X += px;
                    g.Y += py;
                    break;
                }

                // Contacto: quien esté pegado al guardián se congela y el guardián se queda quieto 4 s.
                // Si ya estaba congelado, el tiempo se suma (CA-21.10): red 2 s + guardián 3 s = 5 s.
                foreach (var j in jugadores.Values)
                    if (Math.Abs(j.X - g.X) + Math.Abs(j.Y - g.Y) <= 1)
                    {
                        Congelar(j.Id, SegundosCongelado, "GUARDIAN", null);
                        g.Pausa = 4;
                        break;
                    }
            }

            var eraDobles = doblesSim;
            doblesSim = segundo >= 10 && segundo < 20;
            if (!eraDobles && doblesSim)
                EventoIniciado?.Invoke(new EventoIniciadoMsg
                {
                    IdEvento = IdEventoDemo, Tipo = TiposEvento.PuntosDobles, Duracion = 10,
                    Mensaje = "¡Puntos dobles! Todo vale x2 durante 10 s.",
                });
            if (eraDobles && !doblesSim) EventoTerminado?.Invoke(new EventoTerminadoMsg { IdEvento = IdEventoDemo, Tipo = TiposEvento.PuntosDobles });

            // Energía suelta: unas 6 por partida (una cada 10 s), en un punto al azar, +25 y dura 8 s.
            if (segundo % 10 == 0)
            {
                var hayPuntos = puntosEnergiaSim.Count > 0;
                var (ex, ey) = hayPuntos ? puntosEnergiaSim[azar.Next(puntosEnergiaSim.Count)] : (0, 0);
                itemSim = hayPuntos && EsTransitable(ex, ey) ? new ItemEnergiaMsg { Id = segundo / 10, X = ex, Y = ey, Puntos = 25 } : null;
            }
            if (itemSim != null) itemSim.SegundosRestantes = 8 - segundo % 10;
            if (segundo % 10 >= 8) itemSim = null;

            ArenaActualizada?.Invoke(new ArenaActualizadaMsg { SegundosTranscurridos = segundo, Vivo = ConstruirVivo(segundo) });
        }

        ArenaVivaMsg ConstruirVivo(int segundo)
        {
            var vivo = new ArenaVivaMsg
            {
                MultiplicadorPuntos = doblesSim ? 2 : 1,
                Guardianes = guardianesSim.Select(g => new GuardianMsg { Id = g.Id, X = g.X, Y = g.Y, Pausado = g.Pausa > 0 }).ToList(),
                Trampas = trampasSim.ToList(),
                Puertas = puertasSim
                    .Select((p, i) => new PuertaMsg { X = p.x, Y = p.y, Abierta = PuertaAbierta(i, segundo) })
                    .Where(p => EsTransitable(p.X, p.Y)).ToList(),
                Congelados = congeladosSim.Select(c => new CongeladoMsg { JugadorId = c.Key, Segundos = c.Value }).ToList(),
            };
            if (itemSim != null) vivo.Items.Add(itemSim);
            if (doblesSim)
                vivo.Eventos.Add(new EventoActivoMsg { Id = IdEventoDemo, Tipo = TiposEvento.PuntosDobles, Duracion = 10, SegundosRestantes = 20 - segundo });
            return vivo;
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

        bool EsTransitable(int x, int y)
        {
            if (y < 0 || y >= mapa.Length || x < 0 || x >= mapa[y].Length) return false;
            var celda = mapa[y][x];
            return celda == Celdas.Piso || celda == Celdas.Inicio;
        }

        bool EstaOcupada(int x, int y, string excepto) =>
            jugadores.Values.Any(j => j.Id != excepto && j.X == x && j.Y == y);

        (int x, int y) BuscarReactor()
        {
            for (int y = 0; y < mapa.Length; y++)
                for (int x = 0; x < mapa[y].Length; x++)
                    if (mapa[y][x] == Celdas.Reactor) return (x, y);
            return (-1, -1);
        }

        List<Vector2Int> BuscarInicios()
        {
            var inicios = new List<Vector2Int>();
            for (int y = 0; y < mapa.Length; y++)
                for (int x = 0; x < mapa[y].Length; x++)
                    if (mapa[y][x] == Celdas.Inicio) inicios.Add(new Vector2Int(x, y));
            return inicios;
        }

        static JugadorMsg Copiar(JugadorMsg j) => new JugadorMsg
        {
            Id = j.Id, Nombre = j.Nombre, AvatarUrl = j.AvatarUrl, Color = j.Color, X = j.X, Y = j.Y, Puntaje = j.Puntaje,
        };
    }
}