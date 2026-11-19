using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Infraestructura.Persistencia;
using Infraestructura.Seguridad;

namespace API.Configuracion;

/// <summary>
/// US-29 (Palma): login con GitHub. Usa lo que registra AddRegistroUsuarios (usuarios, avatares, hash y JWT).
/// El ClientId viene de GitHub:ClientId (en Docker: GitHub__ClientId desde GITHUB_CLIENT_ID del .env).
/// </summary>
public static class LoginSocialServicios
{
    public static IServiceCollection AddLoginSocial(this IServiceCollection services, IConfiguration config)
    {
        var carpetaDatos = config["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");

        services.AddSingleton(new OpcionesGitHub(config["GitHub:ClientId"]));
        services.AddHttpClient<IProveedorGitHub, ProveedorGitHub>(cliente => cliente.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton(new JsonFileStore<CuentaExternaRegistro>(Path.Combine(carpetaDatos, "cuentas-externas.json")));
        services.AddSingleton<ICuentaExternaRepo, JsonCuentaExternaRepositorio>();
        services.AddScoped<LoginSocialService>();
        return services;
    }
}
