using System.Security.Claims;
using Aplicacion.CasosDeUso;
using Contratos.Perfil;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>US-04: perfil con estadísticas y logros, historial de partidas y ranking.</summary>
[ApiController, Authorize, Route("api")]
public sealed class PerfilController(PerfilJugadorService perfil) : ControllerBase
{
    [HttpGet("perfil")]
    public async Task<ActionResult<PerfilJugadorDto>> Perfil(CancellationToken ct) =>
        Ok(await perfil.ObtenerPerfilAsync(UserId(), ct));

    [HttpGet("perfil/historial")]
    public async Task<ActionResult<IReadOnlyList<PartidaHistorialDto>>> Historial([FromQuery] int limite = 20, CancellationToken ct = default) =>
        Ok(await perfil.ObtenerHistorialAsync(UserId(), limite, ct));

    [HttpGet("ranking")]
    public async Task<ActionResult<IReadOnlyList<EntradaRankingDto>>> Ranking(
        [FromQuery] string? criterio, [FromQuery] int limite = 10, CancellationToken ct = default) =>
        Ok(await perfil.ObtenerRankingAsync(criterio, limite, ct));

    private Guid UserId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(valor, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
