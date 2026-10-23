using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Infraestructura.Partidas;
using Infraestructura.Persistencia;

namespace API.Configuracion;

public static class HistorialServicios
{
    /// <summary>
    /// US-04. Registra el historial en JSON (DataPath/historial-partidas.json), los casos de uso del perfil y
    /// ENVUELVE el IPartidaRepo ya registrado con <see cref="PartidaRepoConHistorial"/>.
    /// Debe llamarse DESPUÉS de AddPartidas() y de AddRegistroUsuarios() (necesita IPartidaRepo, IUsuarioRepo e IClock).
    /// </summary>
    public static IServiceCollection AddHistorialPerfil(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var carpetaDatos = configuracion["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");

        servicios.AddSingleton(new JsonFileStore<PartidaHistorialRegistro>(Path.Combine(carpetaDatos, "historial-partidas.json")));
        servicios.AddSingleton<IHistorialRepo, JsonHistorialRepositorio>();
        servicios.AddSingleton<RegistrarResultadoPartida>();
        servicios.AddScoped<PerfilJugadorService>();

        var original = servicios.Single(d => d.ServiceType == typeof(IPartidaRepo));
        servicios.Remove(original);
        servicios.AddSingleton<IPartidaRepo>(sp =>
        {
            var interno = original.ImplementationInstance as IPartidaRepo
                ?? (original.ImplementationFactory is not null
                    ? (IPartidaRepo)original.ImplementationFactory(sp)
                    : (IPartidaRepo)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!));

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PartidaRepoConHistorial>();
            return new PartidaRepoConHistorial(
                interno,
                sp.GetRequiredService<RegistrarResultadoPartida>(),
                (id, ex) => logger.LogError(ex, "No se pudo guardar en el historial la partida {PartidaId}", id));
        });

        return servicios;
    }
}
