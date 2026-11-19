namespace API.TiempoReal;

/// <summary>
/// Salas de tiempo real: qué jugadores están conectados a cada partida.
/// Se registra como Singleton (una sola instancia para todo el servidor).
/// US-30: también guarda los espectadores (el móvil). Reciben lo mismo que la sala, pero no juegan.
/// </summary>
public sealed class AdministradorSalas
{
    private readonly Dictionary<Guid, Dictionary<Guid, ConexionJugador>> _salas = new();
    private readonly Dictionary<Guid, HashSet<ConexionJugador>> _espectadores = new();
    private readonly object _candado = new();

    /// <summary>
    /// Agrega la conexión. Si el jugador ya estaba conectado (Unity cambió de escena),
    /// la nueva reemplaza a la anterior.
    /// </summary>
    public void Agregar(Guid partidaId, ConexionJugador conexion)
    {
        lock (_candado)
        {
            if (!_salas.TryGetValue(partidaId, out var sala))
            {
                sala = new Dictionary<Guid, ConexionJugador>();
                _salas[partidaId] = sala;
            }
            sala[conexion.UsuarioId] = conexion;
        }
    }

    /// <summary>Quita la conexión solo si sigue siendo la misma (no borra una reconexión más nueva).</summary>
    public void Quitar(Guid partidaId, ConexionJugador conexion)
    {
        lock (_candado)
        {
            if (!_salas.TryGetValue(partidaId, out var sala))
                return;

            if (sala.TryGetValue(conexion.UsuarioId, out var actual) && ReferenceEquals(actual, conexion))
                sala.Remove(conexion.UsuarioId);

            if (sala.Count == 0)
                _salas.Remove(partidaId);
        }
    }

    /// <summary>
    /// US-30: agrega una conexión que solo mira la partida (el móvil). Va aparte de los jugadores,
    /// así no reemplaza la conexión de Unity del mismo usuario.
    /// </summary>
    public void AgregarEspectador(Guid partidaId, ConexionJugador conexion)
    {
        lock (_candado)
        {
            if (!_espectadores.TryGetValue(partidaId, out var mirando))
            {
                mirando = new HashSet<ConexionJugador>();
                _espectadores[partidaId] = mirando;
            }
            mirando.Add(conexion);
        }
    }

    /// <summary>US-30: el móvil dejó de mirar (cerró la página o cambió de pestaña).</summary>
    public void QuitarEspectador(Guid partidaId, ConexionJugador conexion)
    {
        lock (_candado)
        {
            if (!_espectadores.TryGetValue(partidaId, out var mirando))
                return;

            mirando.Remove(conexion);
            if (mirando.Count == 0)
                _espectadores.Remove(partidaId);
        }
    }

    /// <summary>Envía el mismo mensaje a todos los jugadores conectados a la partida y a quienes la miran.</summary>
    public Task EnviarATodosAsync<T>(Guid partidaId, string tipo, T data, CancellationToken ct = default)
    {
        var destinatarios = new List<ConexionJugador>();
        lock (_candado)
        {
            if (_salas.TryGetValue(partidaId, out var sala))
                destinatarios.AddRange(sala.Values);
            if (_espectadores.TryGetValue(partidaId, out var mirando))
                destinatarios.AddRange(mirando);
        }

        if (destinatarios.Count == 0)
            return Task.CompletedTask;

        var texto = SerializadorMensajes.Serializar(tipo, data);
        return Task.WhenAll(destinatarios.Select(c => c.EnviarTextoAsync(texto, ct)));
    }

    /// <summary>
    /// Envia un mensaje solo a un jugador de la partida (por ejemplo, un rechazo). Si no esta conectado, no hace nada.
    /// Los espectadores no reciben estos mensajes privados.
    /// </summary>
    public Task EnviarAJugadorAsync<T>(Guid partidaId, Guid usuarioId, string tipo, T data, CancellationToken ct = default)
    {
        ConexionJugador? conexion = null;
        lock (_candado)
        {
            if (_salas.TryGetValue(partidaId, out var sala))
                sala.TryGetValue(usuarioId, out conexion);
        }

        return conexion is null ? Task.CompletedTask : conexion.EnviarAsync(tipo, data, ct);
    }
}