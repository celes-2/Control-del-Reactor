using System.Security.Claims;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Contratos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AutenticacionService auth, AvatarPropioService avataresPropios, IDispositivoRepo dispositivos) : ControllerBase
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
        Ok(await auth.ListarAvataresAsync(UserIdOpcional(), ct));

    /// <summary>Recibe los bytes de una imagen PNG o JPG (máximo 1 MB) en el cuerpo y la deja como avatar del jugador.</summary>
    [Authorize, HttpPost("profile/avatar/upload"), RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<UsuarioDto>> SubirAvatar(CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int leidos;
        while ((leidos = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            memoria.Write(buffer, 0, leidos);
            if (memoria.Length > AvatarPropioService.MaxBytes)
                throw new AuthException("IMAGEN_MUY_GRANDE", "La imagen no puede pesar más de 1 MB.");
        }

        var userId = UserId();
        await avataresPropios.SubirAsync(userId, memoria.ToArray(), ct);
        return Ok(await auth.PerfilAsync(userId, ct));
    }

    /// <summary>Imagen de un avatar propio. Es pública para que los demás jugadores vean tu avatar en la partida.</summary>
    [AllowAnonymous, HttpGet("/api/avatares/{id:guid}/imagen")]
    public async Task<IActionResult> Imagen(Guid id, CancellationToken ct)
    {
        var imagen = await avataresPropios.ObtenerImagenAsync(id, ct);
        return imagen is null ? NotFound() : File(imagen.Contenido, imagen.TipoContenido);
    }

    [Authorize, HttpPut("profile/avatar")]
    public async Task<ActionResult<UsuarioDto>> Avatar(ElegirAvatarRequest req, CancellationToken ct) =>
        Ok(await auth.ElegirAvatarAsync(UserId(), req.AvatarId, ct));

    [Authorize, HttpPost("/api/devices/pair")]
    public async Task<ActionResult> Pair(EmparejarDispositivoRequest req, CancellationToken ct)
    {
        await dispositivos.AsignarAsync(req.DeviceId, UserId(), ct);
        return Ok(new { mensaje = "Dispositivo asociado correctamente." });
    }

    private Guid? UserIdOpcional()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("nameid") ?? User.FindFirstValue("sub");
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private Guid UserId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(valor, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
