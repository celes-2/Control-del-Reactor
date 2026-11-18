using Dominio.Comun;
using Dominio.Juego;

namespace PruebasDominio;

/// <summary>Utilidades compartidas por las pruebas de la arena viva (guardianes, trampas, puertas y eventos).</summary>
internal static class AyudaArenaViva
{
    /// <summary>
    /// Mapa de 11x6 para pruebas de guardianes. Ana aparece en (0,0) pegada al reactor (1,0); Beto en (10,0).
    /// Los guardianes empiezan en (0,3) y (10,3): el primero está más cerca de Ana y el segundo de Beto.
    /// </summary>
    public static readonly string[] MapaGuardianes =
    [
        "SR........S",
        "...........",
        "...........",
        "G.........G",
        "...........",
        "..........."
    ];

    /// <summary>Crea una partida con la arena dada, dos jugadores (Ana y Beto), la arena viva activada y ya iniciada.</summary>
    public static (Partida partida, Jugador ana, Jugador beto) Crear(
        IReadOnlyList<string> filas,
        int guardianes = 2,
        int semilla = 1,
        ConfigPartida? config = null,
        bool arenaViva = true)
    {
        var configuracion = (config ?? new ConfigPartida()) with { CantidadGuardianes = guardianes, MaxJugadores = 2 };
        var partida = Partida.Crear(configuracion, Arena.DesdeFilas(filas));
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        partida.Iniciar(arenaViva ? new Random(semilla) : null);
        return (partida, ana, beto);
    }

    /// <summary>Crea una partida sobre un escenario del catálogo, con n jugadores.</summary>
    public static (Partida partida, Jugador[] jugadores) CrearEscenario(
        string id, int jugadores = 2, int guardianes = 4, int semilla = 1, ConfigPartida? config = null)
    {
        // Igual que ConfigurarPartida: el escenario pone su id y sus trampas aleatorias.
        var definicion = Escenarios.Obtener(id);
        var configuracion = definicion.Aplicar((config ?? new ConfigPartida()) with
        {
            CantidadGuardianes = guardianes,
            MaxJugadores = jugadores
        });

        var partida = Partida.Crear(configuracion, definicion.CrearArena());
        var lista = Enumerable.Range(1, jugadores)
            .Select(i => partida.Unirse(Guid.NewGuid(), $"J{i}", Guid.NewGuid()))
            .ToArray();
        partida.Iniciar(new Random(semilla));
        return (partida, lista);
    }

    /// <summary>Avanza el reloj de a un segundo hasta que se cumpla la condición. Devuelve false si se agotaron los segundos.</summary>
    public static bool AvanzarHasta(Partida partida, Func<bool> condicion, int maximo = 600)
    {
        for (var i = 0; i < maximo && partida.Estado == EstadoPartida.EnCurso; i++)
        {
            if (condicion())
                return true;
            partida.Tick(1);
        }
        return condicion();
    }

    /// <summary>
    /// Lleva al jugador hasta la celda destino con movimientos reales (Ejecutar), por el camino más corto
    /// que no pisa trampas ni obstáculos, jugadores ni guardianes. Devuelve el resultado del último movimiento.
    /// El tiempo no avanza mientras camina.
    /// </summary>
    public static ResultadoAccion Caminar(Partida partida, Jugador jugador, Posicion destino)
    {
        var arena = partida.Arena;
        var ocupadas = partida.Jugadores.Where(j => j.Id != jugador.Id).Select(j => j.Posicion)
            .Concat(partida.Guardianes.Select(g => g.Posicion))
            .ToHashSet();

        var previo = new Dictionary<Posicion, (Posicion De, Accion Con)>();
        var cola = new Queue<Posicion>();
        cola.Enqueue(jugador.Posicion);
        previo[jugador.Posicion] = (jugador.Posicion, Accion.Arriba);

        var pasos = new (Direccion Dir, Accion Accion)[]
        {
            (Direccion.Arriba, Accion.Arriba), (Direccion.Abajo, Accion.Abajo),
            (Direccion.Izquierda, Accion.Izquierda), (Direccion.Derecha, Accion.Derecha)
        };

        while (cola.Count > 0 && !previo.ContainsKey(destino))
        {
            var actual = cola.Dequeue();
            foreach (var (dir, accion) in pasos)
            {
                var vecina = actual.Mover(dir);
                var esDestino = vecina == destino;
                var libre = arena.EsTransitable(vecina) && !ocupadas.Contains(vecina) &&
                            (arena.ObstaculoEn(vecina) is null || esDestino || arena.ObstaculoEn(vecina)?.Tipo == TipoObstaculo.Puerta);
                if (previo.ContainsKey(vecina) || !libre)
                    continue;
                previo[vecina] = (actual, accion);
                cola.Enqueue(vecina);
            }
        }

        if (!previo.ContainsKey(destino))
            throw new InvalidOperationException($"No hay camino de {jugador.Posicion} a {destino}.");

        var camino = new List<Accion>();
        for (var celda = destino; celda != jugador.Posicion; celda = previo[celda].De)
            camino.Add(previo[celda].Con);
        camino.Reverse();

        var resultado = ResultadoAccion.Movido;
        foreach (var accion in camino)
            resultado = partida.Ejecutar(jugador.Id, accion);
        return resultado;
    }

    /// <summary>Hay alguien más en esa celda: otro jugador o un guardián.</summary>
    public static bool Ocupada(Partida partida, Posicion posicion) =>
        partida.Jugadores.Any(j => j.Posicion == posicion) || partida.Guardianes.Any(g => g.Posicion == posicion);
}
