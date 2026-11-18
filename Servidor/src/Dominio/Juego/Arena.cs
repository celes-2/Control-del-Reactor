using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Tablero rectangular con un reactor, puntos de aparición y obstáculos.
/// Se puede crear de dos formas:
///  - <c>new Arena(ancho, alto, obstaculos)</c>: reactor al centro y jugadores en las 4 esquinas.
///  - <see cref="DesdeFilas"/>: a partir de un escenario en filas de texto (el mismo formato que usa Unity).
/// Conoce los obstáculos (muros, trampas y puertas), pero no sabe nada de jugadores ni de reglas de juego.
/// </summary>
public sealed class Arena
{
    public const int MaxJugadores = 4;
    public const int TamanoMinimo = 5;

    // Caracteres del escenario.
    public const char CeldaPiso = '.';
    public const char CeldaMuro = 'W';
    public const char CeldaTrampa = 'T';
    public const char CeldaInicio = 'S';
    public const char CeldaReactor = 'R';

    /// <summary>Puerta automática. Empieza abierta o cerrada según su orden de lectura (ver <see cref="ArenaViva"/>).</summary>
    public const char CeldaPuerta = 'D';

    /// <summary>Lugar donde puede aparecer un guardián. Es piso normal para los jugadores.</summary>
    public const char CeldaGuardian = 'G';

    private readonly Dictionary<Posicion, Obstaculo> _obstaculos = new();
    private readonly Dictionary<Posicion, Puerta> _puertas = new();
    private readonly Posicion[] _aparicion;
    private readonly Posicion[] _inicioGuardianes;

    public int Ancho { get; }
    public int Alto { get; }
    public Posicion PosicionReactor { get; }
    public IReadOnlyCollection<Obstaculo> Obstaculos => _obstaculos.Values;
    public IReadOnlyCollection<Puerta> Puertas => _puertas.Values;

    /// <summary>Lugares marcados con G en el escenario, en orden de lectura. Puede estar vacío.</summary>
    public IReadOnlyList<Posicion> InicioGuardianes => _inicioGuardianes;

    /// <summary>Cuántos jugadores caben según los puntos de aparición de esta arena.</summary>
    public int CantidadPuntosDeAparicion => _aparicion.Length;

    /// <summary>Reactor al centro y jugadores en las esquinas.</summary>
    public Arena(int ancho, int alto, IEnumerable<Obstaculo>? obstaculos = null)
        : this(ancho, alto, new Posicion(ancho / 2, alto / 2), Esquinas(ancho, alto), obstaculos, null)
    {
    }

    private Arena(
        int ancho, int alto, Posicion reactor, Posicion[] aparicion,
        IEnumerable<Obstaculo>? obstaculos, IEnumerable<Posicion>? inicioGuardianes)
    {
        if (ancho < TamanoMinimo || alto < TamanoMinimo)
            throw new DominioException($"La arena debe medir al menos {TamanoMinimo}x{TamanoMinimo}.");

        Ancho = ancho;
        Alto = alto;
        PosicionReactor = reactor;
        _aparicion = aparicion;

        foreach (var obstaculo in obstaculos ?? [])
        {
            if (!EstaDentro(obstaculo.Posicion))
                throw new DominioException($"El obstáculo en {obstaculo.Posicion} queda fuera de la arena.");
            if (EsReactor(obstaculo.Posicion))
                throw new DominioException("No se puede colocar un obstáculo sobre el reactor.");
            if (EsPuntoDeAparicion(obstaculo.Posicion))
                throw new DominioException($"No se puede colocar un obstáculo en el punto de aparición {obstaculo.Posicion}.");
            if (!_obstaculos.TryAdd(obstaculo.Posicion, obstaculo))
                throw new DominioException($"Ya hay un obstáculo en {obstaculo.Posicion}.");

            if (obstaculo.Tipo == TipoObstaculo.Puerta)
                _puertas[obstaculo.Posicion] = new Puerta(obstaculo.Posicion, abierta: _puertas.Count % 2 == 1);
        }

        var guardianes = new List<Posicion>();
        foreach (var inicio in inicioGuardianes ?? [])
        {
            if (!EstaDentro(inicio) || EsReactor(inicio) || EsPuntoDeAparicion(inicio) || _obstaculos.ContainsKey(inicio))
                throw new DominioException($"El inicio de guardián en {inicio} no es válido.");
            guardianes.Add(inicio);
        }
        _inicioGuardianes = guardianes.ToArray();
    }

    /// <summary>
    /// Crea la arena a partir de un escenario en filas de texto. Origen (0,0) arriba a la izquierda.
    /// Caracteres: W muro, . piso, S inicio de un jugador, R reactor (exactamente uno), T trampa,
    /// D puerta automática, G lugar donde puede empezar un guardián.
    /// Los jugadores aparecen en las S en orden de lectura (de arriba abajo, de izquierda a derecha).
    /// </summary>
    public static Arena DesdeFilas(IReadOnlyList<string> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        if (filas.Count == 0)
            throw new DominioException("El escenario no tiene filas.");

        var ancho = filas[0]?.Length ?? 0;
        if (filas.Any(f => f is null || f.Length != ancho))
            throw new DominioException("Todas las filas del escenario deben tener el mismo largo.");

        var obstaculos = new List<Obstaculo>();
        var inicios = new List<Posicion>();
        var reactores = new List<Posicion>();
        var guardianes = new List<Posicion>();

        for (var y = 0; y < filas.Count; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                var posicion = new Posicion(x, y);
                switch (filas[y][x])
                {
                    case CeldaPiso:
                        break;
                    case CeldaMuro:
                        obstaculos.Add(new Obstaculo(posicion, TipoObstaculo.Muro));
                        break;
                    case CeldaTrampa:
                        obstaculos.Add(new Obstaculo(posicion, TipoObstaculo.Trampa));
                        break;
                    case CeldaPuerta:
                        obstaculos.Add(new Obstaculo(posicion, TipoObstaculo.Puerta));
                        break;
                    case CeldaInicio:
                        inicios.Add(posicion);
                        break;
                    case CeldaReactor:
                        reactores.Add(posicion);
                        break;
                    case CeldaGuardian:
                        guardianes.Add(posicion);
                        break;
                    default:
                        throw new DominioException(
                            $"Carácter no válido '{filas[y][x]}' en el escenario, en {posicion}. Use W . S R T D G.");
                }
            }
        }

        if (reactores.Count != 1)
            throw new DominioException("El escenario debe tener exactamente un reactor (R).");
        if (inicios.Count < 2 || inicios.Count > MaxJugadores)
            throw new DominioException($"El escenario debe tener entre 2 y {MaxJugadores} puntos de inicio (S).");

        return new Arena(ancho, filas.Count, reactores[0], inicios.ToArray(), obstaculos, guardianes);
    }

    /// <summary>
    /// El escenario como filas de texto, en el mismo formato que acepta <see cref="DesdeFilas"/>.
    /// Las puertas salen como muro (cerrada) o piso (abierta) y los inicios de guardián como piso:
    /// así los clientes que solo conocen W . S R T siguen entendiendo el mapa.
    /// </summary>
    public IReadOnlyList<string> ComoFilas()
    {
        var filas = new List<string>(Alto);
        for (var y = 0; y < Alto; y++)
        {
            var fila = new char[Ancho];
            for (var x = 0; x < Ancho; x++)
            {
                var posicion = new Posicion(x, y);
                fila[x] = EsReactor(posicion) ? CeldaReactor
                    : EsPuntoDeAparicion(posicion) ? CeldaInicio
                    : ObstaculoEn(posicion)?.Tipo switch
                    {
                        TipoObstaculo.Muro => CeldaMuro,
                        TipoObstaculo.Trampa => CeldaTrampa,
                        TipoObstaculo.Puerta => PuertaEn(posicion)?.Abierta == true ? CeldaPiso : CeldaMuro,
                        _ => CeldaPiso
                    };
            }
            filas.Add(new string(fila));
        }
        return filas;
    }

    public bool EstaDentro(Posicion posicion) =>
        posicion.X >= 0 && posicion.X < Ancho && posicion.Y >= 0 && posicion.Y < Alto;

    public bool EsReactor(Posicion posicion) => posicion == PosicionReactor;

    public Obstaculo? ObstaculoEn(Posicion posicion) =>
        _obstaculos.TryGetValue(posicion, out var obstaculo) ? obstaculo : null;

    public Puerta? PuertaEn(Posicion posicion) =>
        _puertas.TryGetValue(posicion, out var puerta) ? puerta : null;

    /// <summary>
    /// Si se puede estar en esa celda ahora mismo: dentro de la arena, sin reactor, sin muro y sin puerta cerrada.
    /// No mira jugadores ni guardianes.
    /// </summary>
    public bool EsTransitable(Posicion posicion)
    {
        if (!EstaDentro(posicion) || EsReactor(posicion))
            return false;

        return ObstaculoEn(posicion)?.Tipo switch
        {
            TipoObstaculo.Muro => false,
            TipoObstaculo.Puerta => PuertaEn(posicion)?.Abierta == true,
            _ => true
        };
    }

    /// <summary>
    /// Punto donde aparece el jugador número <paramref name="indice"/> (0 es el primero).
    /// En una arena normal son las esquinas: superior izquierda, superior derecha, inferior izquierda, inferior derecha.
    /// </summary>
    public Posicion PuntoDeAparicion(int indice)
    {
        if (indice < 0 || indice >= _aparicion.Length)
            throw new ArgumentOutOfRangeException(nameof(indice));
        return _aparicion[indice];
    }

    /// <summary>Celda de piso sin nada especial: ni obstáculo, ni reactor, ni punto de aparición, ni inicio de guardián.</summary>
    internal bool EsCeldaLibre(Posicion posicion) =>
        EstaDentro(posicion) && !EsReactor(posicion) && !EsPuntoDeAparicion(posicion) &&
        !_obstaculos.ContainsKey(posicion) && Array.IndexOf(_inicioGuardianes, posicion) < 0;

    internal bool EsPuntoDeInicio(Posicion posicion) => EsPuntoDeAparicion(posicion);

    /// <summary>Agrega una trampa en una celda libre. Devuelve false si la celda no sirve.</summary>
    internal bool AgregarTrampa(Posicion posicion, TipoTrampa variante)
    {
        if (!EsCeldaLibre(posicion))
            return false;

        _obstaculos[posicion] = new Obstaculo(posicion, TipoObstaculo.Trampa, variante);
        return true;
    }

    private bool EsPuntoDeAparicion(Posicion posicion) => Array.IndexOf(_aparicion, posicion) >= 0;

    private static Posicion[] Esquinas(int ancho, int alto) =>
    [
        new Posicion(0, 0),
        new Posicion(ancho - 1, 0),
        new Posicion(0, alto - 1),
        new Posicion(ancho - 1, alto - 1)
    ];
}
