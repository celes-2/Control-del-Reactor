using Aplicacion.Puertos;
using Dominio.Usuarios;

namespace Infraestructura.Persistencia;

/// <summary>Forma de un token en tokens.json. Solo se guarda el hash del código, nunca el código.</summary>
public sealed class TokenRegistro
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public static TokenRegistro Desde(TokenUsuario t) => new()
    {
        Id = t.Id, UserId = t.UserId, Type = t.Tipo.ToString(), TokenHash = t.TokenHash,
        ExpiresAt = t.ExpiraEn, UsedAt = t.UsadoEn
    };

    public TokenUsuario ADominio() =>
        TokenUsuario.Reconstruir(Id, UserId, Enum.Parse<TipoToken>(Type), TokenHash, ExpiresAt, UsedAt);
}

public sealed class JsonTokenUsuarioRepositorio(JsonFileStore<TokenRegistro> almacen) : ITokenUsuarioRepo
{
    public async Task GuardarAsync(TokenUsuario token, CancellationToken ct = default) =>
        await almacen.ModificarAsync(todos => todos.Add(TokenRegistro.Desde(token)), ct);

    public async Task<TokenUsuario?> ObtenerAsync(string tokenHash, TipoToken tipo, CancellationToken ct = default) =>
        (await almacen.LeerAsync(ct))
            .FirstOrDefault(x => x.TokenHash == tokenHash && x.Type == tipo.ToString())?.ADominio();

    public async Task ActualizarAsync(TokenUsuario token, CancellationToken ct = default) =>
        await almacen.ModificarAsync(todos =>
        {
            var i = todos.FindIndex(x => x.Id == token.Id);
            if (i >= 0) todos[i] = TokenRegistro.Desde(token);
        }, ct);
}

/// <summary>Forma de un control físico en devices.json.</summary>
public sealed class DispositivoRegistro
{
    public string DeviceId { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public sealed class JsonDispositivoRepositorio(JsonFileStore<DispositivoRegistro> almacen, IClock reloj) : IDispositivoRepo
{
    public async Task AsignarAsync(string deviceId, Guid userId, CancellationToken ct = default)
    {
        var dispositivo = new DispositivoAsignado(deviceId, userId, reloj.UtcNow); // valida el deviceId
        await almacen.ModificarAsync(todos =>
        {
            // Un control con un solo jugador y un jugador con un solo control (CA-10.7).
            todos.RemoveAll(x => string.Equals(x.DeviceId, dispositivo.DeviceId, StringComparison.OrdinalIgnoreCase) || x.UserId == userId);
            todos.Add(new DispositivoRegistro { DeviceId = dispositivo.DeviceId, UserId = userId, LastSeenAt = dispositivo.VistoEn });
        }, ct);
    }

    public async Task<DispositivoAsignado?> ObtenerPorDeviceIdAsync(string deviceId, CancellationToken ct = default)
    {
        var r = (await almacen.LeerAsync(ct))
            .FirstOrDefault(x => string.Equals(x.DeviceId, deviceId?.Trim(), StringComparison.OrdinalIgnoreCase));
        return r is null ? null : new DispositivoAsignado(r.DeviceId, r.UserId, r.LastSeenAt);
    }
}
