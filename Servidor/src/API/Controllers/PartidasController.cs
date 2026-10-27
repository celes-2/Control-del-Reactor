using API.Mapeos;
using API.Seguridad;
using Aplicacion.CasosDeUso;
using Contratos.Partidas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Capa delgada: traduce HTTP a casos de uso y viceversa. No contiene reglas del juego.
/// Los errores (DominioException -> 400, NoEncontradoException -> 404, sin sesión -> 401) los convierte el middleware.
/// El jugador sale del JWT; el "usuarioId" del cuerpo solo se acepta en modo provisional (Auth:PermitirUserIdProvisional).
/// </summary>
[ApiController]
[Route("api/partidas")]
public sealed class PartidasController(
    ConfigurarPartida configurarPartida,
    UnirsePartida unirsePartida,
    IniciarPartida iniciarPartida,
    ObtenerPartida obtenerPartida,
    ListarPartidasEnEspera listarPartidas,
    ResolutorIdentidad identidad) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PartidaRespuesta>> Crear(
        CrearPartidaSolicitud solicitud, CancellationToken ct)
    {
        var comando = new CrearPartidaComando(
            UsuarioDe(solicitud.UsuarioId),
            PartidaMapeo.AConfig(solicitud),
            PartidaMapeo.AObstaculos(solicitud.Obstaculos));

        var vista = await configurarPartida.EjecutarAsync(comando, ct);
        return CreatedAtAction(nameof(Obtener), new { id = vista.Id }, vista.ARespuesta());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartidaResumenRespuesta>>> ListarEnEspera(CancellationToken ct)
    {
        var resumenes = await listarPartidas.EjecutarAsync(ct);
        return Ok(resumenes.Select(r => r.ARespuesta()).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartidaRespuesta>> Obtener(Guid id, CancellationToken ct)
    {
        var vista = await obtenerPartida.EjecutarAsync(id, ct);
        return Ok(vista.ARespuesta());
    }

    [HttpPost("{id:guid}/unirse")]
    public async Task<ActionResult<PartidaRespuesta>> Unirse(
        Guid id, UnirsePartidaSolicitud solicitud, CancellationToken ct)
    {
        var vista = await unirsePartida.EjecutarAsync(new UnirsePartidaComando(id, UsuarioDe(solicitud.UsuarioId)), ct);
        return Ok(vista.ARespuesta());
    }

    [HttpPost("{id:guid}/iniciar")]
    public async Task<ActionResult<PartidaRespuesta>> Iniciar(
        Guid id, IniciarPartidaSolicitud solicitud, CancellationToken ct)
    {
        var vista = await iniciarPartida.EjecutarAsync(new IniciarPartidaComando(id, UsuarioDe(solicitud.UsuarioId)), ct);
        return Ok(vista.ARespuesta());
    }

    /// <summary>Con token válido manda el token; sin token solo sirve el id del cuerpo si el modo provisional está permitido.</summary>
    private Guid UsuarioDe(Guid usuarioDelCuerpo)
    {
        if (identidad.Resolver(HttpContext) is { } id)
            return id;

        if (identidad.PermiteProvisional && usuarioDelCuerpo != Guid.Empty)
            return usuarioDelCuerpo;

        throw new UnauthorizedAccessException();
    }
}
