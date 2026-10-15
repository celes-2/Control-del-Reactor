using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Raíz del agregado: reúne arena, reactor y jugadores, y es la única que aplica las reglas.
/// Es una clase pura: no usa reloj, archivos, red ni base de datos. El tiempo avanza
/// cuando la capa de aplicación llama a <see cref="Tick"/>, lo que la hace fácil de probar.
///
/// Reglas:
///  - Los jugadores aparecen en las esquinas. Hay entre 2 y 4.
///  - Los movimientos son de una celda. Muros, bordes, el reactor y otros jugadores bloquean.
///  - Cada movimiento efectivo suma 1 punto (US-15).
///  - El fin se elige en la configuración (US-08): por TIEMPO o por PUNTOS.
///      Tiempo: al llegar a cero gana quien tenga más puntos.
///      Puntos: gana el primero que llegue a PuntosObjetivo.
///  - Empate al acabarse el tiempo: se suman 2 minutos y la partida sigue con los mismos puntajes (CA-16.4).
///  - Un jugador congelado no puede actuar hasta que pase su tiempo de congelamiento.
///  - "Especial" (quinto botón) no cambia el estado: solo avisa que el jugador hizo una acción (US-14).
///  - Extras del Sprint 3 ya implementados: trampas (quitan vida; el último vivo gana) y extracción de energía.
/// </summary>
public sealed class Partida
{
    private readonly List<Jugador> _jugadores = [];

    public Guid Id { get; }
    public ConfigPartida Config { get; }
    public Arena Arena { get; }
    public Reactor Reactor { get; }
    public EstadoPartida Estado { get; private set; } = EstadoPartida.Espera;
    public int SegundosRestantes { get; private set; }

    /// <summary>Segundos que se suman cuando el tiempo se acaba en empate.</summary>
    public const int SegundosPorEmpate = 120;

    /// <summary>Cuántas veces se extendió la partida por empate.</summary>
    public int Prorrogas { get; private set; }

    /// <summary>Id del jugador ganador. Es null mientras se juega o si terminó en empate.</summary>
    public Guid? GanadorId { get; private set; }

    public IReadOnlyList<Jugador> Jugadores => _jugadores;

    public Partida(Guid id, ConfigPartida config, IEnumerable<Obstaculo>? obstaculos = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validar();

        Id = id;
        Config = config;
        Arena = new Arena(config.Ancho, config.Alto, obstaculos);
        Reactor = new Reactor(Arena.PosicionReactor, config.CapacidadReactor);
        SegundosRestantes = config.DuracionSegundos;
    }

    public static Partida Crear(ConfigPartida config, IEnumerable<Obstaculo>? obstaculos = null) =>
        new(Guid.NewGuid(), config, obstaculos);

    public Jugador Unirse(Guid usuarioId, string nombre, Guid avatarId)
    {
        if (Estado != EstadoPartida.Espera)
            throw new DominioException("Solo se puede entrar a una partida que no ha comenzado.");
        if (_jugadores.Count >= Config.MaxJugadores)
            throw new DominioException("La partida está llena.");
        if (_jugadores.Any(j => j.Id == usuarioId))
            throw new DominioException("El usuario ya está en la partida.");
        if (_jugadores.Any(j => j.AvatarId == avatarId))
            throw new DominioException("Ese avatar ya fue elegido por otro jugador.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DominioException("El jugador necesita un nombre.");

        var jugador = new Jugador(
            usuarioId, nombre.Trim(), avatarId,
            Arena.PuntoDeAparicion(_jugadores.Count),
            Config.VidaInicial);

        _jugadores.Add(jugador);
        return jugador;
    }

    public void Iniciar()
    {
        if (Estado != EstadoPartida.Espera)
            throw new DominioException("La partida ya comenzó.");
        if (_jugadores.Count < 2)
            throw new DominioException("Se necesitan al menos 2 jugadores para comenzar.");

        Estado = EstadoPartida.EnCurso;
    }

    public ResultadoAccion Ejecutar(Guid jugadorId, Accion accion)
    {
        if (Estado != EstadoPartida.EnCurso)
            throw new DominioException("La partida no está en curso.");

        var jugador = _jugadores.FirstOrDefault(j => j.Id == jugadorId)
            ?? throw new DominioException("El jugador no pertenece a esta partida.");

        if (jugador.Eliminado)
            throw new DominioException("El jugador está eliminado.");

        if (jugador.Congelado)
            return ResultadoAccion.Congelado;

        return accion switch
        {
            Accion.Extraer => Extraer(jugador),
            Accion.Especial => ResultadoAccion.AccionRealizada,
            _ => Mover(jugador, accion.ADireccion())
        };
    }

    /// <summary>Congela a un jugador durante Config.SegundosCongelamiento (el inmovilizador del enunciado).</summary>
    public void Congelar(Guid jugadorId)
    {
        if (Estado != EstadoPartida.EnCurso)
            throw new DominioException("La partida no está en curso.");

        var jugador = _jugadores.FirstOrDefault(j => j.Id == jugadorId)
            ?? throw new DominioException("El jugador no pertenece a esta partida.");

        jugador.Congelar(Config.SegundosCongelamiento);
    }

    /// <summary>Avanza el tiempo: recarga el reactor, descongela jugadores y revisa si terminó por tiempo.</summary>
    public void Tick(int segundos)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segundos);

        if (Estado != EstadoPartida.EnCurso)
            return;

        Reactor.Recargar(Config.RegeneracionPorSegundo * segundos);
        foreach (var jugador in _jugadores)
            jugador.AvanzarTiempo(segundos);

        if (Config.ModoFin != ModoFin.Tiempo)
            return;

        SegundosRestantes = Math.Max(0, SegundosRestantes - segundos);

        if (SegundosRestantes == 0)
            FinalizarPorTiempo();
    }

    private ResultadoAccion Mover(Jugador jugador, Direccion direccion)
    {
        var destino = jugador.Posicion.Mover(direccion);

        if (!Arena.EstaDentro(destino) || Arena.EsReactor(destino))
            return ResultadoAccion.Bloqueado;

        if (_jugadores.Any(j => !j.Eliminado && j.Id != jugador.Id && j.Posicion == destino))
            return ResultadoAccion.Bloqueado;

        var obstaculo = Arena.ObstaculoEn(destino);
        if (obstaculo?.Tipo == TipoObstaculo.Muro)
            return ResultadoAccion.Bloqueado;

        jugador.MoverA(destino);
        jugador.SumarPunto();

        if (obstaculo?.Tipo == TipoObstaculo.Trampa)
        {
            jugador.RecibirDano(Config.DanoTrampa);
            if (jugador.Eliminado)
                Reactor.Recargar(jugador.VaciarEnergia());

            EvaluarFin();
            return ResultadoAccion.DanoPorTrampa;
        }

        EvaluarFin();
        return ResultadoAccion.Movido;
    }

    private ResultadoAccion Extraer(Jugador jugador)
    {
        if (!Reactor.EstaAdyacente(jugador.Posicion))
            return ResultadoAccion.LejosDelReactor;

        var energia = Reactor.Extraer(Config.EnergiaPorExtraccion);
        if (energia == 0)
            return ResultadoAccion.ReactorVacio;

        jugador.GanarEnergia(energia);
        return ResultadoAccion.EnergiaExtraida;
    }

    private void EvaluarFin()
    {
        if (Estado != EstadoPartida.EnCurso)
            return;

        var vivos = _jugadores.Where(j => !j.Eliminado).ToList();

        if (Config.ModoFin == ModoFin.Puntos)
        {
            var meta = vivos.FirstOrDefault(j => j.Puntaje >= Config.PuntosObjetivo);
            if (meta is not null)
            {
                Terminar(meta.Id);
                return;
            }
        }

        if (vivos.Count <= 1)
            Terminar(vivos.Count == 1 ? vivos[0].Id : null);
    }

    private void FinalizarPorTiempo()
    {
        var vivos = _jugadores.Where(j => !j.Eliminado).ToList();
        if (vivos.Count == 0)
        {
            Terminar(null);
            return;
        }

        var maxima = vivos.Max(j => j.Puntaje);
        var lideres = vivos.Where(j => j.Puntaje == maxima).ToList();

        if (lideres.Count == 1)
        {
            Terminar(lideres[0].Id);
            return;
        }

        // Empate: se suman 2 minutos y la partida continúa conservando los puntajes.
        SegundosRestantes = SegundosPorEmpate;
        Prorrogas++;
    }

    private void Terminar(Guid? ganadorId)
    {
        Estado = EstadoPartida.Finalizada;
        GanadorId = ganadorId;
    }
}
