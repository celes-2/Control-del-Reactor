using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Infraestructura.Seguridad;
using Microsoft.IdentityModel.Tokens;

namespace API.Seguridad;

/// <summary>
/// Un solo lugar para saber QUIÉN hace una petición (REST o WebSocket), en este orden:
///  1) el JWT del encabezado "Authorization: Bearer ..." (ya validado por el middleware de autenticación);
///  2) el JWT en la URL "?token=..." (el WebSocket no puede mandar encabezados desde Unity);
///  3) solo si "Auth:PermitirUserIdProvisional" es true (por defecto sí, para desarrollo y pruebas
///     sin login): "?userId=GUID" o el id que venga en el cuerpo. Para la entrega con login, ponerlo en false.
/// </summary>
public sealed class ResolutorIdentidad(JwtOpciones jwt, bool permitirProvisional)
{
    public bool PermiteProvisional => permitirProvisional;

    /// <summary>Id del jugador, o null si no se pudo identificar.</summary>
    public Guid? Resolver(HttpContext contexto)
    {
        if (IdDe(contexto.User) is { } desdeEncabezado)
            return desdeEncabezado;

        var token = contexto.Request.Query["token"].ToString();
        if (!string.IsNullOrWhiteSpace(token))
            return ValidarToken(token);   // si el token viene pero es inválido, NO se cae al modo provisional

        if (permitirProvisional && Guid.TryParse(contexto.Request.Query["userId"].ToString(), out var provisional))
            return provisional;

        return null;
    }

    private static Guid? IdDe(ClaimsPrincipal? usuario)
    {
        var valor = usuario?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? usuario?.FindFirstValue("nameid")
                    ?? usuario?.FindFirstValue("sub");
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private Guid? ValidarToken(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
            }, out _);
            return IdDe(principal);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
