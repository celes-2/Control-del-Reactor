namespace Contratos.Auth;

// Contratos REST: no dependen del Dominio para que Unity pueda consumirlos.
public sealed record RegistroRequest(string Username, string Email, string Password, string ConfirmPassword);
public sealed record VerificarCorreoRequest(string Token);
public sealed record LoginRequest(string UsernameOrEmail, string Password);
public sealed record RecuperarContrasenaRequest(string Email);
public sealed record CambiarContrasenaRequest(string Token, string NewPassword, string ConfirmPassword);
public sealed record ElegirAvatarRequest(Guid AvatarId);
public sealed record EmparejarDispositivoRequest(string DeviceId);
public sealed record UsuarioDto(Guid Id, string Username, string Email, bool EmailVerified, Guid? AvatarId);
public sealed record AvatarDto(Guid Id, string Nombre, string Color);
public sealed record LoginResponse(string AccessToken, UsuarioDto User);
public sealed record ErrorResponse(string Codigo, string Mensaje);
