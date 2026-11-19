using System;
using System.Collections.Generic;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Un escenario elegible al crear la partida (US-23), tal como lo entrega GET /api/escenarios.
    /// Filas: 'W' pared, '.' piso, 'S' salida de jugador, 'R' reactor, 'T' trampa (también se acepta '#' en datos falsos).
    /// </summary>
    public sealed class EscenarioInfo
    {
        /// <summary>El mapa clásico del servidor: no manda escenarioId.</summary>
        public static readonly EscenarioInfo Clasico = new EscenarioInfo
        {
            Id = "", Nombre = "Centro de investigación", Descripcion = "El mapa por defecto del servidor.",
        };

        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";

        /// <summary>Define colores y adornos: investigacion, laboratorio, estacion o militar.</summary>
        public string Tema { get; set; } = "";

        /// <summary>Nombres de las imágenes de piso y pared (vacío = colores del tema).</summary>
        public string TilePiso { get; set; } = "";
        public string TilePared { get; set; } = "";
        public IReadOnlyList<string> Filas { get; set; } = Array.Empty<string>();
        public IReadOnlyList<(int x, int y)> Puertas { get; set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int x, int y)> InicioGuardianes { get; set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int x, int y)> Trampas { get; set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int x, int y)> PuntosEnergia { get; set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int x, int y)> Decoracion { get; set; } = Array.Empty<(int, int)>();

        /// <summary>0 = sin límite conocido.</summary>
        public int MaxJugadores { get; set; }
        public int MinGuardianes { get; set; } = 2;
        public int MaxGuardianes { get; set; } = 4;
        public int GuardianesPorDefecto { get; set; } = 4;
    }
}
