using Aplicacion;
using Aplicacion.CasosDeUso;
using Dominio.Comun;

namespace API.Middleware;

/// <summary>
/// Convierte los errores esperados en respuestas { "error": "...", "codigo": "...", "mensaje": "..." }.
/// "error" y "mensaje" llevan el mismo texto: así el cliente de partidas (que lee "error") y el de
/// autenticación (que lee "codigo"/"mensaje") funcionan con el mismo middleware.
/// Regla incumplida o AuthException -> 400, inexistente -> 404, sin sesión -> 401, otro -> 500.
/// </summary>
public sealed class ManejadorErrores(RequestDelegate siguiente, ILogger<ManejadorErrores> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception ex) when (!contexto.Response.HasStarted)
        {
            var (estado, codigo, mensaje) = ex switch
            {
                AuthException a => (StatusCodes.Status400BadRequest, a.Codigo, a.Message),
                NoEncontradoException n => (StatusCodes.Status404NotFound, "NO_ENCONTRADO", n.Message),
                DominioException d => (StatusCodes.Status400BadRequest, "REGLA_DE_NEGOCIO", d.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "NO_AUTORIZADO", "Debes iniciar sesión."),
                _ => (StatusCodes.Status500InternalServerError, "ERROR_INTERNO", "Ocurrió un error inesperado.")
            };

            if (estado == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Error no controlado");

            contexto.Response.StatusCode = estado;
            await contexto.Response.WriteAsJsonAsync(new { error = mensaje, codigo, mensaje });
        }
    }
}
