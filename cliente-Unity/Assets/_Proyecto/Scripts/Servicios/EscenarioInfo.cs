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
            Id = "", Nombre = "Mapa clásico", Descripcion = "El mapa de siempre.",
        };

        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public IReadOnlyList<string> Filas { get; set; } = Array.Empty<string>();
        public IReadOnlyList<(int x, int y)> Puertas { get; set; } = Array.Empty<(int, int)>();
        public IReadOnlyList<(int x, int y)> InicioGuardianes { get; set; } = Array.Empty<(int, int)>();

        /// <summary>0 = sin límite conocido.</summary>
        public int MaxJugadores { get; set; }
        public int MinGuardianes { get; set; } = 2;
        public int MaxGuardianes { get; set; } = 4;
        public int GuardianesPorDefecto { get; set; } = 4;
    }
}
