using System.Security.Claims;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Contratos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AutenticacionService auth, IDispositivoRepo dispositivos) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult> Register(RegistroRequest req, CancellationToken ct) =>
        Created("/api/auth/login", await auth.RegistrarAsync(req, ct));

    [HttpPost("verify-email")]
    public async Task<ActionResult> Verify(VerificarCorreoRequest req, CancellationToken ct)
    {
        await auth.VerificarCorreoAsync(req.Token, ct);
        return Ok(new { mensaje = "Correo verificado correctamente." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct) =>
        Ok(await auth.LoginAsync(req, ct));

    [HttpPost("forgot-password")]
    public async Task<ActionResult> Forgot(RecuperarContrasenaRequest req, CancellationToken ct)
    {
        await auth.SolicitarRecuperacionAsync(req.Email, ct);
        return Ok(new { mensaje = "Si existe una cuenta con ese correo, recibirás instrucciones." });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> Reset(CambiarContrasenaRequest req, CancellationToken ct)
    {
        await auth.CambiarContrasenaAsync(req, ct);
        return Ok(new { mensaje = "Contraseña actualizada correctamente." });
    }

    [Authorize, HttpGet("profile")]
    public async Task<ActionResult<UsuarioDto>> Profile(CancellationToken ct) =>
        Ok(await auth.PerfilAsync(UserId(), ct));

    [HttpGet("/api/avatares")]
    public async Task<ActionResult<IReadOnlyList<AvatarDto>>> Avatares(CancellationToken ct) =>
        Ok(await auth.ListarAvataresAsync(ct));

    [Authorize, HttpPut("profile/avatar")]
    public async Task<ActionResult<UsuarioDto>> Avatar(ElegirAvatarRequest req, CancellationToken ct) =>
        Ok(await auth.ElegirAvatarAsync(UserId(), req.AvatarId, ct));

    [Authorize, HttpPost("/api/devices/pair")]
    public async Task<ActionResult> Pair(EmparejarDispositivoRequest req, CancellationToken ct)
    {
        await dispositivos.AsignarAsync(req.DeviceId, UserId(), ct);
        return Ok(new { mensaje = "Dispositivo asociado correctamente." });
    }

    private Guid UserId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(valor, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
