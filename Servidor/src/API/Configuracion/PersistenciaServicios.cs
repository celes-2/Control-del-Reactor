using Aplicacion.Puertos;
using Infraestructura.Persistencia;

namespace API.Configuracion;

public static class PersistenciaServicios
{
    /// <summary>
    /// Registra los repositorios que guardan en archivos JSON.
    /// Las carpetas salen de la configuración: "DataPath" (datos de ejecución, en Docker es un volumen)
    /// y "SeedPath" (datos iniciales que se versionan en Servidor/Data/seed).
    /// </summary>
    public static IServiceCollection AddPersistenciaJson(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var carpetaDatos = configuracion["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        var carpetaSemilla = configuracion["SeedPath"] ?? Path.Combine(AppContext.BaseDirectory, "seed");

        // Singleton: cada archivo debe tener una sola instancia para que el candado sirva.
        servicios.AddSingleton<IUsuarioRepo>(_ =>
            new JsonUsuarioRepositorio(
                Path.Combine(carpetaDatos, "usuarios.json"),
                Path.Combine(carpetaSemilla, "usuarios.json")));

        servicios.AddSingleton<IAvatarRepo>(_ =>
            new JsonAvatarRepositorio(
                Path.Combine(carpetaDatos, "avatares.json"),
                Path.Combine(carpetaSemilla, "avatares.json")));

        return servicios;
    }
}
