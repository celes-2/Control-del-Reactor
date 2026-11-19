using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// La parte "viva" de la arena (Paquete B, US-20 a US-22): trampas colocadas al azar, puertas automáticas,
/// guardianes que persiguen jugadores, energía adicional y eventos especiales.
/// Vive dentro de la <see cref="Partida"/>, que es la única que la crea y le da el tiempo (un segundo a la vez).
/// Es dominio puro: no usa reloj ni red, y toda la aleatoriedad sale del <see cref="Random"/> que recibe,
/// así que con una semilla fija se puede probar paso a paso.
///
/// Reglas (documentadas en Docs/arena-viva.md):
///  - Al empezar se colocan al azar las trampas aleatorias (cuántas, lo dice el escenario), repartidas entre las
///    3 variantes, separadas entre sí, lejos del reactor, de los inicios y de los puntos de energía.
///  - Los guardianes empiezan en los extremos (celdas G del escenario, o los puntos medios de los bordes).
///  - Un guardián da un paso cada SegundosPorPasoGuardian hacia el jugador no congelado más cercano.
///    Si queda a una celda de un jugador, lo congela; si ya estaba congelado, le suma tiempo (CA-21.10).
///    Después se queda quieto SegundosPausaGuardian. Alcanzar a un jugador nunca cambia su puntaje (CA-21.9).
///  - Las puertas cambian de estado cada SegundosCicloPuertas, cada una con un desfase distinto, y nunca se
///    cierran sobre alguien.
///  - Eventos programados al azar durante toda la partida: energía extra (6), sobrecarga (2), invasión de
///    guardianes, puntos dobles y rutas alternativas (o lluvia de energía si no hay puertas).
///    Un evento no se activa dos veces a la vez: si ya está activo, se pospone.
/// </summary>
internal sealed class ArenaViva
{
    private static readonly Direccion[] Direcciones =
        [Direccion.Arriba, Direccion.Abajo, Direccion.Izquierda, Direccion.Derecha];

    private readonly Partida _partida;
    private readonly Random _azar;
    private readonly ReglasArenaViva _reglas;
    private readonly List<Guardian> _guardianes = [];
    private readonly List<ItemEnergia> _items = [];
    private readonly List<EventoActivo> _eventos = [];
    private readonly List<(int Segundo, TipoEvento Tipo)> _calendario = [];

    private int _proximoGuardian = 1;
    private int _proximoItem = 1;
    private bool _terminada;

    public IReadOnlyList<Guardian> Guardianes => _guardianes;
    public IReadOnlyList<ItemEnergia> Items => _items;
    public IReadOnlyList<EventoActivo> Eventos => _eventos;

    /// <summary>Lo que se ve en el calendario de eventos pendientes (para pruebas y diagnóstico).</summary>
    public IReadOnlyList<(int Segundo, TipoEvento Tipo)> Calendario => _calendario;

    /// <summary>Por cuánto se multiplican los puntos que se ganan ahora (2 durante "puntos dobles").</summary>
    public int Multiplicador =>
        _eventos.Any(e => e.Tipo == TipoEvento.PuntosDobles) ? _reglas.MultiplicadorPuntosDobles : 1;

    public ArenaViva(Partida partida, Random azar)
    {
        _partida = partida;
        _azar = azar;
        _reglas = partida.Config.ArenaViva;
    }

    // ------------------------------------------------------------------ preparación

    public void Activar()
    {
        ColocarTrampas();
        CrearGuardianesIniciales();

        // Mitad abiertas y mitad cerradas, y cada una cambia en un segundo distinto (no todas a la vez).
        var ciclo = _reglas.SegundosCicloPuertas;
        var indice = 0;
        foreach (var puerta in _partida.Arena.Puertas.OrderBy(p => p.Posicion.Y).ThenBy(p => p.Posicion.X))
        {
            puerta.Reiniciar(abierta: indice % 2 == 1, segundosParaCambio: ciclo - indice % ciclo);
            indice++;
        }

        ProgramarLote(desde: 0);
    }

    /// <summary>
    /// Coloca las trampas aleatorias (CA-20.9). Primero busca en las celdas marcadas con t (si el escenario
    /// las tiene) y después en el resto del piso libre, siempre al azar. Ninguna queda pegada al reactor, a un
    /// inicio de jugador o de guardián, ni sobre un punto de energía. Entre trampas se respeta la distancia mínima;
    /// si el escenario no alcanza, se acercan lo necesario. Las variantes se reparten parejo.
    /// </summary>
    private void ColocarTrampas()
    {
        var arena = _partida.Arena;
        var cantidad = _reglas.TrampasAleatorias;

        bool Permitida(Posicion c) =>
            arena.EsCeldaLibre(c) && !arena.PuntosEnergia.Contains(c) &&
            c.DistanciaManhattan(arena.PosicionReactor) >= 2 && LejosDeInicios(arena, c, 3) &&
            arena.InicioGuardianes.All(g => g.DistanciaManhattan(c) >= 2);

        var preferidas = arena.CeldasParaTrampas.Where(Permitida).ToList();
        var resto = CeldasLibres(arena).Where(c => Permitida(c) && !preferidas.Contains(c)).ToList();
        Mezclar(preferidas);
        Mezclar(resto);
        var candidatas = preferidas.Concat(resto).ToList();

        var elegidas = new List<Posicion>();
        for (var distancia = _reglas.DistanciaMinimaEntreTrampas; distancia >= 1 && elegidas.Count < cantidad; distancia--)
        {
            foreach (var celda in candidatas)
            {
                if (elegidas.Count == cantidad)
                    break;
                if (!elegidas.Contains(celda) && elegidas.All(e => e.DistanciaManhattan(celda) >= distancia))
                    elegidas.Add(celda);
            }
        }

        if (elegidas.Count < cantidad)
            throw new DominioException("La arena no tiene espacio para colocar las trampas.");

        // Variantes repartidas parejo y en orden al azar: con 3 trampas sale una de cada una.
        var variantes = Enum.GetValues<TipoTrampa>().ToList();
        Mezclar(variantes);
        for (var i = 0; i < elegidas.Count; i++)
            arena.AgregarTrampa(elegidas[i], variantes[i % variantes.Count]);
    }

    private static bool LejosDeInicios(Arena arena, Posicion celda, int distancia)
    {
        for (var i = 0; i < arena.CantidadPuntosDeAparicion; i++)
            if (arena.PuntoDeAparicion(i).DistanciaManhattan(celda) < distancia)
                return false;
        return true;
    }

    private static IEnumerable<Posicion> CeldasLibres(Arena arena)
    {
        for (var y = 0; y < arena.Alto; y++)
            for (var x = 0; x < arena.Ancho; x++)
            {
                var celda = new Posicion(x, y);
                if (arena.EsCeldaLibre(celda))
                    yield return celda;
            }
    }

    private void CrearGuardianesIniciales()
    {
        foreach (var inicio in ElegirInicios(_partida.Config.CantidadGuardianes))
            _guardianes.Add(NuevoGuardian(inicio, temporal: false));
    }

    /// <summary>
    /// Elige dónde empiezan N guardianes, repartidos en los extremos. Usa las celdas G del escenario si hay
    /// suficientes; si no, el piso libre más cercano a los puntos medios de los cuatro bordes.
    /// </summary>
    private List<Posicion> ElegirInicios(int cantidad)
    {
        var arena = _partida.Arena;

        var origen = arena.InicioGuardianes.Count >= cantidad
            ? arena.InicioGuardianes.ToList()
            : PuntosMediosDeBordes(arena);

        var elegidas = new List<Posicion>();
        for (var i = 0; i < cantidad; i++)
        {
            // Repartidas parejo: con 2 salen la primera y la última; con 4 salen las 4.
            var indice = cantidad == 1 ? 0 : (int)Math.Round(i * (origen.Count - 1) / (double)(cantidad - 1));
            var candidata = origen[Math.Min(indice, origen.Count - 1)];
            elegidas.Add(arena.EsCeldaLibre(candidata) || arena.InicioGuardianes.Contains(candidata)
                ? candidata
                : CeldaLibreMasCercana(arena, candidata, elegidas));
        }
        return elegidas;
    }

    private static List<Posicion> PuntosMediosDeBordes(Arena arena) =>
    [
        new Posicion(arena.Ancho / 2, 0),
        new Posicion(0, arena.Alto / 2),
        new Posicion(arena.Ancho - 1, arena.Alto / 2),
        new Posicion(arena.Ancho / 2, arena.Alto - 1)
    ];

    private static Posicion CeldaLibreMasCercana(Arena arena, Posicion desde, List<Posicion> ocupadas) =>
        CeldasLibres(arena)
            .Where(c => !ocupadas.Contains(c) && arena.EsTransitable(c))
            .OrderBy(c => c.DistanciaManhattan(desde))
            .ThenBy(c => c.Y).ThenBy(c => c.X)
            .FirstOrDefault(desde);

    private Guardian NuevoGuardian(Posicion posicion, bool temporal) =>
        new(_proximoGuardian++, posicion, temporal, _reglas.SegundosPorPasoGuardian);

    // ------------------------------------------------------------------ calendario de eventos

    /// <summary>
    /// Programa un lote de eventos repartidos en la duración de la partida, con algo de azar en el momento.
    /// Cuando se acaba el lote se programa otro, así una partida por puntos (sin tiempo límite) o con prórroga
    /// sigue teniendo eventos.
    /// </summary>
    private void ProgramarLote(int desde)
    {
        var tipos = new List<TipoEvento>();
        tipos.AddRange(Enumerable.Repeat(TipoEvento.EnergiaExtra, _reglas.VecesEnergiaExtra));
        tipos.AddRange(Enumerable.Repeat(TipoEvento.SobrecargaReactor, _reglas.VecesSobrecarga));
        tipos.Add(TipoEvento.InvasionGuardianes);
        tipos.Add(TipoEvento.PuntosDobles);
        tipos.Add(_partida.Arena.Puertas.Count > 0 ? TipoEvento.RutasAlternativas : TipoEvento.LluviaEnergia);
        Mezclar(tipos);

        var horizonte = Math.Max(_partida.Config.DuracionSegundos, tipos.Count + 1);
        var hueco = Math.Max(2, horizonte / (tipos.Count + 1));
        var ultimo = desde;

        for (var i = 0; i < tipos.Count; i++)
        {
            var variacion = _azar.Next(-(hueco / 4), hueco / 4 + 1);
            var segundo = Math.Max(ultimo + 2, desde + hueco * (i + 1) + variacion);
            _calendario.Add((segundo, tipos[i]));
            ultimo = segundo;
        }
    }

    // ------------------------------------------------------------------ paso del tiempo

    /// <summary>Avanza un segundo. <paramref name="segundoActual"/> es el segundo de la partida que acaba de empezar.</summary>
    public void AvanzarUnSegundo(int segundoActual)
    {
        if (_terminada)
            return;

        AvanzarEventos();
        AvanzarPuertas();
        DispararProgramados(segundoActual);
        MoverGuardianes();
    }

    private void AvanzarEventos()
    {
        foreach (var evento in _eventos.ToList())
        {
            evento.SegundosRestantes--;
            if (evento.SegundosRestantes <= 0)
                Terminar(evento);
        }
    }

    private void AvanzarPuertas()
    {
        var forzada = _eventos.Any(e => e.Tipo == TipoEvento.RutasAlternativas);
        foreach (var puerta in _partida.Arena.Puertas)
            puerta.AvanzarUnSegundo(_reglas.SegundosCicloPuertas, forzada, HayAlguienEn(puerta.Posicion));
    }

    private void DispararProgramados(int segundo)
    {
        var pospuestos = new List<(int Segundo, TipoEvento Tipo)>();

        while (_calendario.Count > 0 && _calendario[0].Segundo <= segundo)
        {
            var (_, tipo) = _calendario[0];
            _calendario.RemoveAt(0);

            // Un evento no se aplica dos veces a la vez (CA-22.9): si no se pudo activar, se pospone.
            if (!Iniciar(tipo))
                pospuestos.Add((segundo + 2, tipo));
        }

        if (pospuestos.Count > 0)
        {
            _calendario.AddRange(pospuestos);
            _calendario.Sort((a, b) => a.Segundo.CompareTo(b.Segundo));
        }

        if (_calendario.Count == 0)
            ProgramarLote(segundo);
    }

    // ------------------------------------------------------------------ eventos

    private bool Iniciar(TipoEvento tipo)
    {
        if (_eventos.Any(e => e.Tipo == tipo))
            return false;

        switch (tipo)
        {
            case TipoEvento.EnergiaExtra:
            {
                var celda = ElegirCeldaParaItem();
                if (celda is null)
                    return false;

                var evento = Empezar(tipo, _reglas.SegundosEnergiaExtra);
                _items.Add(new ItemEnergia(_proximoItem++, celda.Value, _reglas.PuntosEnergiaExtra, evento.Id));
                return true;
            }

            case TipoEvento.LluviaEnergia:
            {
                var celdas = new List<Posicion>();
                for (var i = 0; i < _reglas.CeldasLluviaEnergia; i++)
                {
                    var celda = ElegirCeldaParaItem(celdas);
                    if (celda is not null)
                        celdas.Add(celda.Value);
                }
                if (celdas.Count == 0)
                    return false;

                var evento = Empezar(tipo, _reglas.SegundosLluviaEnergia);
                foreach (var celda in celdas)
                    _items.Add(new ItemEnergia(_proximoItem++, celda, _reglas.PuntosCeldaLluvia, evento.Id));
                return true;
            }

            case TipoEvento.InvasionGuardianes:
            {
                var celdas = ElegirCeldasParaInvasores(_reglas.GuardianesInvasion);
                if (celdas.Count == 0)
                    return false;

                Empezar(tipo, _reglas.SegundosInvasion);
                foreach (var celda in celdas)
                    _guardianes.Add(NuevoGuardian(celda, temporal: true));
                return true;
            }

            case TipoEvento.SobrecargaReactor:
                // Se usa la entrada que ya ofrece la Partida: mientras dure, nadie puede extraer.
                _partida.DesactivarReactor(_reglas.SegundosSobrecarga);
                Empezar(tipo, _reglas.SegundosSobrecarga);
                return true;

            case TipoEvento.PuntosDobles:
                Empezar(tipo, _reglas.SegundosPuntosDobles);
                return true;

            case TipoEvento.RutasAlternativas:
                if (_partida.Arena.Puertas.Count == 0)
                    return true;   // sin puertas no hay nada que abrir: se descarta
                Empezar(tipo, _reglas.SegundosRutasAlternativas);
                foreach (var puerta in _partida.Arena.Puertas)   // el efecto se ve al instante, no un segundo después
                    puerta.Reiniciar(abierta: true, _reglas.SegundosCicloPuertas);
                return true;

            default:
                return true;
        }
    }

    private EventoActivo Empezar(TipoEvento tipo, int segundos)
    {
        var evento = new EventoActivo(tipo, segundos);
        _eventos.Add(evento);
        _partida.Registrar(new EventoIniciadoNovedad(evento.Id, tipo, segundos));
        return evento;
    }

    /// <summary>Termina un evento y deja la arena como estaba (CA-22.7).</summary>
    private void Terminar(EventoActivo evento)
    {
        _eventos.Remove(evento);

        switch (evento.Tipo)
        {
            case TipoEvento.InvasionGuardianes:
                _guardianes.RemoveAll(g => g.Temporal);
                break;
            case TipoEvento.EnergiaExtra:
            case TipoEvento.LluviaEnergia:
                _items.RemoveAll(i => i.EventoId == evento.Id);
                break;
        }

        _partida.Registrar(new EventoTerminadoNovedad(evento.Id, evento.Tipo));
    }

    /// <summary>La partida terminó: ningún evento sigue afectándola (CA-22.8).</summary>
    public void Finalizar()
    {
        if (_terminada)
            return;

        foreach (var evento in _eventos.ToList())
            Terminar(evento);

        _terminada = true;
    }

    /// <summary>
    /// Una celda al azar para la energía: entre los puntos E del escenario si tiene; si no hay (o están todos
    /// ocupados), en cualquier piso libre. Nunca donde ya hay alguien u otra energía.
    /// </summary>
    private Posicion? ElegirCeldaParaItem(IReadOnlyCollection<Posicion>? yaElegidas = null)
    {
        var arena = _partida.Arena;
        bool Sirve(Posicion c) =>
            !HayAlguienEn(c) && _items.All(i => i.Posicion != c) && (yaElegidas is null || !yaElegidas.Contains(c));

        var candidatas = arena.PuntosEnergia.Where(Sirve).ToList();
        if (candidatas.Count == 0)
            candidatas = CeldasLibres(arena).Where(Sirve).ToList();

        return candidatas.Count == 0 ? null : candidatas[_azar.Next(candidatas.Count)];
    }

    /// <summary>Los invasores aparecen lejos del reactor y de los jugadores, para no congelar a nadie al instante.</summary>
    private List<Posicion> ElegirCeldasParaInvasores(int cantidad)
    {
        var arena = _partida.Arena;
        var libres = CeldasLibres(arena)
            .Where(c => arena.EsTransitable(c) && !HayAlguienEn(c))
            .ToList();

        var lejos = libres
            .Where(c => _partida.Jugadores.All(j => j.Eliminado || j.Posicion.DistanciaManhattan(c) >= 3))
            .ToList();
        var pool = lejos.Count >= cantidad ? lejos : libres;

        // Entre las más alejadas del reactor, al azar.
        var candidatas = pool
            .OrderByDescending(c => c.DistanciaManhattan(arena.PosicionReactor))
            .Take(Math.Max(cantidad * 3, cantidad))
            .ToList();
        Mezclar(candidatas);

        return candidatas.Take(cantidad).ToList();
    }

    // ------------------------------------------------------------------ jugadores

    public bool HayGuardianEn(Posicion posicion) => _guardianes.Any(g => g.Posicion == posicion);

    private bool HayAlguienEn(Posicion posicion) =>
        HayGuardianEn(posicion) || _partida.Jugadores.Any(j => !j.Eliminado && j.Posicion == posicion);

    /// <summary>
    /// Si el jugador está parado sobre una energía adicional, la quita de la arena y la devuelve para que la
    /// <see cref="Partida"/> le dé los puntos (los puntos solo los cambia la Partida). Null si no había nada.
    /// El primero que llega se la queda: el ítem desaparece y, si era el último de su evento, el evento termina.
    /// </summary>
    public ItemEnergia? TomarItem(Jugador jugador)
    {
        var item = _items.FirstOrDefault(i => i.Posicion == jugador.Posicion);
        if (item is null)
            return null;

        _items.Remove(item);

        // La energía adicional termina cuando la toman. La lluvia sigue hasta que se acabe su tiempo o la última celda.
        var evento = _eventos.FirstOrDefault(e => e.Id == item.EventoId);
        if (evento is not null && _items.All(i => i.EventoId != evento.Id))
            Terminar(evento);

        return item;
    }

    /// <summary>Un jugador acaba de moverse: si quedó junto a un guardián activo, este lo alcanza.</summary>
    public void RevisarContacto(Jugador jugador)
    {
        if (jugador.Eliminado)
            return;

        var guardian = _guardianes.FirstOrDefault(g =>
            g.SegundosPausa == 0 && g.Posicion.DistanciaManhattan(jugador.Posicion) == 1);

        if (guardian is not null)
            Alcanzar(guardian, jugador);
    }

    private void Alcanzar(Guardian guardian, Jugador jugador)
    {
        var segundos = _partida.Config.SegundosCongelamiento;

        if (segundos > 0)
        {
            // Si ya estaba inmovilizado (por una trampa, por ejemplo), el tiempo se suma (CA-21.10).
            if (jugador.Congelado)
                jugador.ExtenderCongelamiento(segundos);
            else
                jugador.Congelar(segundos);

            _partida.Registrar(new JugadorCongeladoNovedad(
                jugador.Id, jugador.SegundosCongelado, OrigenCongelamiento.Guardian, null));
        }

        guardian.SegundosPausa = _reglas.SegundosPausaGuardian;
    }

    // ------------------------------------------------------------------ guardianes

    private void MoverGuardianes()
    {
        foreach (var guardian in _guardianes.OrderBy(g => g.Id).ToList())
        {
            if (guardian.SegundosPausa > 0)
            {
                guardian.SegundosPausa--;
                continue;
            }

            guardian.SegundosParaPaso--;
            if (guardian.SegundosParaPaso > 0)
                continue;
            guardian.SegundosParaPaso = _reglas.SegundosPorPasoGuardian;

            DarPaso(guardian);
            TocarAdyacente(guardian);
        }
    }

    /// <summary>Un paso hacia el jugador no congelado más cercano (CA-21.11). Si todos están congelados, se queda.</summary>
    private void DarPaso(Guardian guardian)
    {
        var objetivo = _partida.Jugadores
            .Where(j => !j.Eliminado && !j.Congelado)
            .OrderBy(j => j.Posicion.DistanciaManhattan(guardian.Posicion))
            .ThenBy(j => j.Id)
            .FirstOrDefault();

        if (objetivo is null || guardian.Posicion.DistanciaManhattan(objetivo.Posicion) <= 1)
            return;

        var paso = PrimerPasoHacia(guardian, objetivo.Posicion);
        if (paso is not null)
            guardian.MoverA(paso.Value);
    }

    private void TocarAdyacente(Guardian guardian)
    {
        var cercanos = _partida.Jugadores
            .Where(j => !j.Eliminado && j.Posicion.DistanciaManhattan(guardian.Posicion) == 1)
            .ToList();

        var victima = cercanos.FirstOrDefault(j => !j.Congelado) ?? cercanos.FirstOrDefault();
        if (victima is not null)
            Alcanzar(guardian, victima);
    }

    /// <summary>
    /// Búsqueda en anchura sobre las celdas por las que el guardián puede pasar (sin muros, puertas cerradas,
    /// reactor, otros guardianes ni jugadores) hasta quedar a una celda del objetivo. Devuelve la primera celda del camino.
    /// </summary>
    private Posicion? PrimerPasoHacia(Guardian guardian, Posicion objetivo)
    {
        var arena = _partida.Arena;
        var bloqueadas = new HashSet<Posicion>(
            _guardianes.Where(g => g.Id != guardian.Id).Select(g => g.Posicion));
        foreach (var jugador in _partida.Jugadores.Where(j => !j.Eliminado))
            bloqueadas.Add(jugador.Posicion);

        var cola = new Queue<Posicion>();
        var vino = new Dictionary<Posicion, Posicion>();
        cola.Enqueue(guardian.Posicion);
        vino[guardian.Posicion] = guardian.Posicion;

        while (cola.Count > 0)
        {
            var actual = cola.Dequeue();

            if (actual != guardian.Posicion && actual.DistanciaManhattan(objetivo) == 1)
            {
                var paso = actual;
                while (vino[paso] != guardian.Posicion)
                    paso = vino[paso];
                return paso;
            }

            foreach (var direccion in Direcciones)
            {
                var vecina = actual.Mover(direccion);
                if (vino.ContainsKey(vecina) || !arena.EsTransitable(vecina) || bloqueadas.Contains(vecina))
                    continue;

                vino[vecina] = actual;
                cola.Enqueue(vecina);
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ utilidades

    private void Mezclar<T>(IList<T> lista)
    {
        for (var i = lista.Count - 1; i > 0; i--)
        {
            var j = _azar.Next(i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
    }
}
