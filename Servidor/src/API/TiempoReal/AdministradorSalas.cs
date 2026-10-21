namespace API.TiempoReal;

/// <summary>
/// Salas de tiempo real: qué jugadores están conectados a cada partida.
/// Se registra como Singleton (una sola instancia para todo el servidor).
/// </summary>
public sealed class AdministradorSalas
{
    private readonly Dictionary<Guid, Dictionary<Guid, ConexionJugador>> _salas = new();
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

    /// <summary>Envía el mismo mensaje a todos los jugadores conectados a la partida.</summary>
    public Task EnviarATodosAsync<T>(Guid partidaId, string tipo, T data, CancellationToken ct = default)
    {
        List<ConexionJugador> destinatarios;
        lock (_candado)
        {
            if (!_salas.TryGetValue(partidaId, out var sala))
                return Task.CompletedTask;
            destinatarios = sala.Values.ToList();
        }

        var texto = SerializadorMensajes.Serializar(tipo, data);
        return Task.WhenAll(destinatarios.Select(c => c.EnviarTextoAsync(texto, ct)));
    }
}