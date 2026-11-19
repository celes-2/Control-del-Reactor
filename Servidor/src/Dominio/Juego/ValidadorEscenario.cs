using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Revisa que un escenario se pueda jugar (CA-23.2, CA-23.5) antes de ofrecerlo:
///  - el mapa se puede leer (rectangular, un reactor, de 2 a 4 inicios, caracteres conocidos) y está cerrado por muros;
///  - el reactor tiene sus 4 vecinas libres (si no, nadie podría extraer);
///  - hay al menos 4 inicios de guardián (CA-21.1);
///  - ninguna puerta ni trampa fija queda pegada al reactor o a un inicio de jugador;
///  - con todas las puertas cerradas, cada jugador puede llegar al reactor, y con todas abiertas no queda
///    ninguna zona encerrada;
///  - la cantidad de trampas aleatorias está entre 3 y 60.
/// Devuelve la lista de problemas (vacía si está todo bien).
/// </summary>
public static class ValidadorEscenario
{
    public const int MinimoGuardianes = ConfigPartida.MaximoGuardianes;

    public static IReadOnlyList<string> Revisar(EscenarioDef escenario)
    {
        ArgumentNullException.ThrowIfNull(escenario);
        var errores = new List<string>();

        Arena arena;
        try
        {
            arena = escenario.CrearArena();
        }
        catch (DominioException e)
        {
            return [e.Message];
        }

        var reactor = arena.PosicionReactor;
        var inicios = Enumerable.Range(0, arena.CantidadPuntosDeAparicion).Select(arena.PuntoDeAparicion).ToList();

        if (!BordeCerrado(arena))
            errores.Add("El borde del mapa debe ser todo muro (W).");

        if (Vecinas(reactor).Any(v => !arena.EstaDentro(v) || arena.ObstaculoEn(v) is not null))
            errores.Add("Las 4 celdas vecinas del reactor deben ser piso.");

        if (arena.InicioGuardianes.Count < MinimoGuardianes)
            errores.Add($"Hacen falta al menos {MinimoGuardianes} inicios de guardián (G).");

        foreach (var obstaculo in arena.Obstaculos.Where(o => o.Tipo != TipoObstaculo.Muro))
        {
            var p = obstaculo.Posicion;
            if (p.DistanciaManhattan(reactor) < 2 || inicios.Any(i => i.DistanciaManhattan(p) < 2))
                errores.Add($"La {(obstaculo.Tipo == TipoObstaculo.Puerta ? "puerta" : "trampa")} en {p} está pegada al reactor o a una salida.");
        }

        foreach (var inicio in inicios)
        {
            var conPuertasCerradas = Alcanzables(arena, inicio, puertasAbiertas: false);
            if (!Vecinas(reactor).Any(conPuertasCerradas.Contains))
                errores.Add($"Desde el inicio {inicio} no se llega al reactor con las puertas cerradas.");
        }

        var conPuertasAbiertas = Alcanzables(arena, inicios[0], puertasAbiertas: true);
        var encerradas = Pisables(arena).Where(c => !conPuertasAbiertas.Contains(c)).ToList();
        if (encerradas.Count > 0)
            errores.Add($"La celda {encerradas[0]} queda encerrada: ningún jugador puede llegar.");

        if (escenario.TrampasAleatorias < ReglasArenaViva.MinimoTrampas || escenario.TrampasAleatorias > ReglasArenaViva.MaximoTrampas)
            errores.Add($"Las trampas aleatorias deben ser entre {ReglasArenaViva.MinimoTrampas} y {ReglasArenaViva.MaximoTrampas}.");
        if (escenario.DistanciaMinimaEntreTrampas < 1 || escenario.DistanciaMinimaEntreTrampas > 10)
            errores.Add("La distancia mínima entre trampas debe ser entre 1 y 10.");

        return errores;
    }

    private static bool BordeCerrado(Arena arena)
    {
        for (var x = 0; x < arena.Ancho; x++)
            if (!EsMuro(arena, new Posicion(x, 0)) || !EsMuro(arena, new Posicion(x, arena.Alto - 1)))
                return false;
        for (var y = 0; y < arena.Alto; y++)
            if (!EsMuro(arena, new Posicion(0, y)) || !EsMuro(arena, new Posicion(arena.Ancho - 1, y)))
                return false;
        return true;
    }

    private static bool EsMuro(Arena arena, Posicion p) => arena.ObstaculoEn(p)?.Tipo == TipoObstaculo.Muro;

    private static IEnumerable<Posicion> Vecinas(Posicion p) =>
        [p.Mover(Direccion.Arriba), p.Mover(Direccion.Abajo), p.Mover(Direccion.Izquierda), p.Mover(Direccion.Derecha)];

    private static IEnumerable<Posicion> Pisables(Arena arena)
    {
        for (var y = 0; y < arena.Alto; y++)
            for (var x = 0; x < arena.Ancho; x++)
            {
                var p = new Posicion(x, y);
                if (!arena.EsReactor(p) && !EsMuro(arena, p))
                    yield return p;
            }
    }

    private static HashSet<Posicion> Alcanzables(Arena arena, Posicion desde, bool puertasAbiertas)
    {
        bool Pasa(Posicion p) =>
            arena.EstaDentro(p) && !arena.EsReactor(p) && !EsMuro(arena, p) &&
            (puertasAbiertas || arena.ObstaculoEn(p)?.Tipo != TipoObstaculo.Puerta);

        var vistas = new HashSet<Posicion> { desde };
        var cola = new Queue<Posicion>([desde]);
        while (cola.Count > 0)
            foreach (var vecina in Vecinas(cola.Dequeue()))
                if (Pasa(vecina) && vistas.Add(vecina))
                    cola.Enqueue(vecina);
        return vistas;
    }
}
