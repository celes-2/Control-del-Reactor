using API.Mapeos;
using Aplicacion.CasosDeUso;
using Contratos.Partidas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Capa delgada: traduce HTTP a casos de uso y viceversa. No contiene reglas del juego.
/// Los errores (DominioException -> 400, NoEncontradoException -> 404) los convierte el middleware.
/// </summary>
[ApiController]
[Route("api/partidas")]
public sealed class PartidasController(
    ConfigurarPartida configurarPartida,
    UnirsePartida unirsePartida,
    IniciarPartida iniciarPartida,
    ObtenerPartida obtenerPartida,
    ListarPartidasEnEspera listarPartidas) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PartidaRespuesta>> Crear(
        CrearPartidaSolicitud solicitud, CancellationToken ct)
    {
        var comando = new CrearPartidaComando(
            solicitud.UsuarioId,
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
        var vista = await unirsePartida.EjecutarAsync(new UnirsePartidaComando(id, solicitud.UsuarioId), ct);
        return Ok(vista.ARespuesta());
    }

    [HttpPost("{id:guid}/iniciar")]
    public async Task<ActionResult<PartidaRespuesta>> Iniciar(
        Guid id, IniciarPartidaSolicitud solicitud, CancellationToken ct)
    {
        var vista = await iniciarPartida.EjecutarAsync(new IniciarPartidaComando(id, solicitud.UsuarioId), ct);
        return Ok(vista.ARespuesta());
    }
}
