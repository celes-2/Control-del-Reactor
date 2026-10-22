using Dominio.Usuarios;

namespace Aplicacion.Puertos;

public interface ITokenUsuarioRepo
{
    Task GuardarAsync(TokenUsuario token, CancellationToken ct = default);
    Task<TokenUsuario?> ObtenerAsync(string tokenHash, TipoToken tipo, CancellationToken ct = default);
    Task ActualizarAsync(TokenUsuario token, CancellationToken ct = default);
}

public interface IDispositivoRepo
{
    /// <summary>Asocia el control al jugador. Un control tiene un solo jugador y un jugador un solo control.</summary>
    Task AsignarAsync(string deviceId, Guid userId, CancellationToken ct = default);

    /// <summary>Lo usa el adaptador MQTT: si devuelve null, el evento se descarta.</summary>
    Task<DispositivoAsignado?> ObtenerPorDeviceIdAsync(string deviceId, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string contrasena);
    bool Verificar(string contrasena, string hash);
}

public interface ITokenService
{
    string GenerarTokenSeguro();
    string Hash(string valor);
    string CrearJwt(Usuario usuario);
}

public interface IMailSender
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}
