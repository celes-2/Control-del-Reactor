using System.Security.Cryptography;
using System.Text;
using Aplicacion.CasosDeUso;
using API.Seguridad;
using Aplicacion.Puertos;
using Infraestructura.Email;
using Infraestructura.Persistencia;
using Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.Configuracion;

/// <summary>
/// Único lugar (junto con Program.cs) que conoce las clases concretas del módulo de usuarios.
/// Persistencia: archivos JSON en DataPath (usuarios.json, tokens.json, dispositivos.json). Sin base de datos.
/// </summary>
public static class RegistroUsuariosExtensions
{
    public static IServiceCollection AddRegistroUsuarios(this IServiceCollection services, IConfiguration config, bool esDesarrollo = false)
    {
        var carpetaDatos = config["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");

        // IUsuarioRepo e IAvatarRepo ya los registra AddPersistenciaJson (usuarios.json y avatares.json).
        // Aquí solo se agregan los archivos propios de autenticación: un JsonFileStore (Singleton) por archivo.
        services.AddSingleton(new JsonFileStore<TokenRegistro>(Path.Combine(carpetaDatos, "tokens.json")));
        services.AddSingleton(new JsonFileStore<DispositivoRegistro>(Path.Combine(carpetaDatos, "dispositivos.json")));
        services.AddSingleton<IAvatarPropioRepo>(_ => new ArchivoAvatarPropioRepositorio(carpetaDatos));
        services.AddSingleton<ITokenUsuarioRepo, JsonTokenUsuarioRepositorio>();
        services.AddSingleton<IDispositivoRepo, JsonDispositivoRepositorio>();

        services.AddSingleton(ResolverJwt(config, esDesarrollo));
        services.AddSingleton(sp => new ResolutorIdentidad(
            sp.GetRequiredService<JwtOpciones>(), config.GetValue("Auth:PermitirUserIdProvisional", true)));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IClock, RelojSistema>();
        services.AddSingleton<IMailSender>(_ => CrearMailSender(config, carpetaDatos));
        services.AddScoped<AutenticacionService>();
        services.AddScoped<AvatarPropioService>();
        return services;
    }

    /// <summary>Valida los JWT con las mismas opciones con las que JwtTokenService los firma.</summary>
    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOpciones>((opciones, jwt) => opciones.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
            });
        services.AddAuthorization();
        return services;
    }

    private static JwtOpciones ResolverJwt(IConfiguration config, bool esDesarrollo)
    {
        var secreto = config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secreto))
        {
            // Sin secretos en el repositorio: solo en desarrollo se inventa uno temporal (los tokens
            // dejan de servir al reiniciar). Fuera de desarrollo, falta de secreto = error al arrancar.
            if (!esDesarrollo)
                throw new InvalidOperationException("Falta Jwt__Secret. Defínelo en .env o en las variables del pipeline.");
            secreto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }
        if (Encoding.UTF8.GetByteCount(secreto) < 32)
            throw new InvalidOperationException("Jwt__Secret debe tener al menos 32 caracteres.");

        return new JwtOpciones(config["Jwt:Issuer"] ?? "ControlDelReactor", config["Jwt:Audience"] ?? "Unity", secreto);
    }

    private static IMailSender CrearMailSender(IConfiguration config, string carpetaDatos)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            return new ArchivoMailSender(Path.Combine(carpetaDatos, "outbox"));

        return new MailKitMailSender(new SmtpOpciones(
            host,
            config.GetValue("Smtp:Port", 1025),
            config["Smtp:From"] ?? "noreply@controlreactor.local",
            config["Smtp:Username"] ?? config["Smtp:User"],
            config["Smtp:Password"]));
    }
}
