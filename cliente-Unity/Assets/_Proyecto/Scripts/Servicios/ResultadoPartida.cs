using System;
using System.Collections.Generic;
using System.Linq;
using Reactor.Cliente.Contratos;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Un jugador en la pantalla de victoria: lo que se necesita para mostrarlo.</summary>
    public sealed class JugadorResultado
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Color { get; set; }
        public string AvatarUrl { get; set; }
        public string AvatarId { get; set; }
        public int Puntaje { get; set; }

        /// <summary>Posición en que llegó a la partida; sirve para elegir su imagen igual que en la arena.</summary>
        public int OrdenLlegada { get; set; }

        public bool EsGanador { get; set; }
    }

    /// <summary>
    /// Resultado de una partida terminada, con los jugadores ya ordenados por puntaje
    /// (de mayor a menor). Une el mensaje match:finished con los datos de color y avatar
    /// que el cliente ya conocía por match:state.
    /// </summary>
    public sealed class ResultadoPartida
    {
        public IReadOnlyList<JugadorResultado> Jugadores { get; }

        public ResultadoPartida(IEnumerable<JugadorResultado> jugadores)
        {
            Jugadores = (jugadores ?? Enumerable.Empty<JugadorResultado>())
                .OrderByDescending(j => j.Puntaje)
                .ToList();
        }

        public JugadorResultado Ganador => Jugadores.FirstOrDefault(j => j.EsGanador);

        public bool HayGanador => Ganador != null;

        public static ResultadoPartida Crear(PartidaFinalizadaMsg mensaje, IEnumerable<JugadorMsg> jugadores)
        {
            if (mensaje == null) throw new ArgumentNullException(nameof(mensaje));

            var conocidos = new Dictionary<string, JugadorMsg>();
            var orden = new Dictionary<string, int>();
            foreach (var j in jugadores ?? Enumerable.Empty<JugadorMsg>())
            {
                if (string.IsNullOrEmpty(j?.Id) || conocidos.ContainsKey(j.Id)) continue;
                orden[j.Id] = conocidos.Count;
                conocidos[j.Id] = j;
            }

            var lista = new List<JugadorResultado>();
            foreach (var p in mensaje.Puntajes ?? new List<PuntajeFinalMsg>())
            {
                if (p == null) continue;
                var id = p.JugadorId ?? "";
                conocidos.TryGetValue(id, out var conocido);

                lista.Add(new JugadorResultado
                {
                    Id = id,
                    Nombre = string.IsNullOrEmpty(p.Nombre) ? conocido?.Nombre : p.Nombre,
                    Color = conocido?.Color,
                    AvatarUrl = conocido?.AvatarUrl,
                    AvatarId = conocido?.AvatarId,
                    Puntaje = p.Puntaje,
                    OrdenLlegada = orden.TryGetValue(id, out var o) ? o : lista.Count,
                    EsGanador = !string.IsNullOrEmpty(mensaje.GanadorId) && id == mensaje.GanadorId,
                });
            }

            return new ResultadoPartida(lista);
        }
    }
}
