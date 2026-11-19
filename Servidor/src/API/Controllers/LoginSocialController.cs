using Aplicacion.CasosDeUso;
using Contratos.Auth;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// US-29: login con GitHub (device flow). Los errores salen con el formato único { error, codigo, mensaje }:
/// LOGIN_SOCIAL_NO_CONFIGURADO, LOGIN_SOCIAL_CANCELADO, LOGIN_SOCIAL_EXPIRADO, LOGIN_SOCIAL_SIN_CORREO, LOGIN_SOCIAL_FALLO.
/// </summary>
[ApiController, Route("api/auth/github")]
public sealed class LoginSocialController(LoginSocialService login) : ControllerBase
{
    /// <summary>Paso 1: pide a GitHub un código. Unity muestra UserCode y abre VerificationUri.</summary>
    [HttpPost("iniciar")]
    public async Task<ActionResult<GitHubInicioResponse>> Iniciar(CancellationToken ct) =>
        Ok(await login.IniciarAsync(ct));

    /// <summary>Paso 2: Unity pregunta cada Interval segundos hasta recibir COMPLETADO (con el JWT) o un error.</summary>
    [HttpPost("consultar")]
    public async Task<ActionResult<GitHubConsultaResponse>> Consultar(GitHubConsultaRequest req, CancellationToken ct) =>
        Ok(await login.ConsultarAsync(req.DeviceCode, ct));
}
