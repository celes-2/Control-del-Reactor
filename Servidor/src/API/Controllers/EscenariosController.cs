using API.Mapeos;
using Aplicacion.CasosDeUso;
using Contratos.Partidas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Catálogo de escenarios (US-23). Unity lo usa para el selector de escenario antes de crear la partida;
/// el id elegido viaja en CrearPartidaSolicitud.EscenarioId. Es público: no cambia nada en el servidor.
/// </summary>
[ApiController]
[Route("api/escenarios")]
public sealed class EscenariosController(ListarEscenarios listarEscenarios) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<EscenarioRespuesta>> Listar() =>
        Ok(listarEscenarios.Ejecutar().Select(e => e.ARespuesta()).ToList());
}
