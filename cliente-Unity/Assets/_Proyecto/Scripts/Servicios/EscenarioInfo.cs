using System;
using System.Collections.Generic;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Un escenario que se puede elegir al crear la partida (US-23). Las filas son solo para la vista previa:
    /// '#' pared, '.' piso, 'R' reactor, 'A'/'B' salidas de los jugadores, 'T' trampa.
    /// </summary>
    public sealed class EscenarioInfo
    {
        public string Id { get; }
        public string Nombre { get; }
        public IReadOnlyList<string> Filas { get; }

        public EscenarioInfo(string id, string nombre, IReadOnlyList<string> filas)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Nombre = nombre ?? id;
            Filas = filas ?? Array.Empty<string>();
        }
    }

    /// <summary>De dónde sale la lista de escenarios. Hoy es fija; luego vendrá del servidor.</summary>
    public interface ICatalogoEscenarios
    {
        IReadOnlyList<EscenarioInfo> Todos { get; }
    }

    /// <summary>
    /// Catálogo PROVISIONAL con datos de prueba, para desarrollar el selector sin depender del servidor.
    /// Cuando exista el catálogo real (GET de escenarios), se reemplaza por una implementación que lo consulte
    /// y los ids deben coincidir con los del servidor.
    /// </summary>
    public sealed class CatalogoEscenariosPrueba : ICatalogoEscenarios
    {
        public static readonly CatalogoEscenariosPrueba Instancia = new CatalogoEscenariosPrueba();

        public IReadOnlyList<EscenarioInfo> Todos { get; } = new[]
        {
            new EscenarioInfo("clasico", "Clásico", new[]
            {
                "###############",
                "#A...........B#",
                "#.##.......##.#",
                "#.#....T....#.#",
                "#....#...#....#",
                "#..T...R...T..#",
                "#....#...#....#",
                "#.#....T....#.#",
                "#.##.......##.#",
                "#.............#",
                "###############",
            }),
            new EscenarioInfo("laberinto", "Laberinto", new[]
            {
                "###############",
                "#A..#.....#..B#",
                "#.#.#.###.#.#.#",
                "#.#...#T#...#.#",
                "#.#####.#####.#",
                "#T.....R.....T#",
                "#.#####.#####.#",
                "#.#...#T#...#.#",
                "#.#.#.###.#.#.#",
                "#...#.....#...#",
                "###############",
            }),
            new EscenarioInfo("abierto", "Arena abierta", new[]
            {
                "###############",
                "#A...........B#",
                "#.............#",
                "#....T...T....#",
                "#.............#",
                "#......R......#",
                "#.............#",
                "#....T...T....#",
                "#.............#",
                "#.............#",
                "###############",
            }),
        };
    }
}
