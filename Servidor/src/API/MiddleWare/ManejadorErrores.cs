using Aplicacion;
using Dominio.Comun;

namespace API.Middleware;

/// <summary>
/// Convierte los errores esperados en respuestas HTTP claras:
/// regla del juego incumplida -> 400, recurso inexistente -> 404.
/// Cualquier otra excepción sigue su camino normal y termina en 500.
/// </summary>
public sealed class ManejadorErrores(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (NoEncontradoException ex)
        {
            await Responder(contexto, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (DominioException ex)
        {
            await Responder(contexto, StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private static Task Responder(HttpContext contexto, int codigo, string mensaje)
    {
        contexto.Response.StatusCode = codigo;
        return contexto.Response.WriteAsJsonAsync(new { error = mensaje });
    }
}
