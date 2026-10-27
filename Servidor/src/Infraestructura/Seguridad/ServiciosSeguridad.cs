using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aplicacion.Puertos;
using Dominio.Usuarios;
using Microsoft.IdentityModel.Tokens;

namespace Infraestructura.Seguridad;

/// <summary>Configuración del JWT. El secreto viene de .env / variables del pipeline, nunca del código.</summary>
public sealed record JwtOpciones(string Issuer, string Audience, string Secret, int HorasDeVida = 8);

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string contrasena) => BCrypt.Net.BCrypt.HashPassword(contrasena);

    public bool Verificar(string contrasena, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(contrasena, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; } // hash corrupto o vacío: no es válido
    }
}

public sealed class JwtTokenService(JwtOpciones opciones) : ITokenService
{
    public string GenerarTokenSeguro() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public string GenerarCodigoNumerico() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string Hash(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    public string CrearJwt(Usuario usuario)
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Secret)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            opciones.Issuer,
            opciones.Audience,
            [new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()), new Claim(ClaimTypes.Name, usuario.Nombre)],
            expires: DateTime.UtcNow.AddHours(opciones.HorasDeVida),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class RelojSistema : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
