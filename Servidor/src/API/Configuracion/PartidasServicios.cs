using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Infraestructura.Partidas;

namespace API.Configuracion;

public static class PartidasServicios
{
    /// <summary>
    /// Registra los casos de uso de partidas y el almacén en memoria.
    /// Requiere que IUsuarioRepo ya esté registrado (AddPersistenciaJson, módulo registro).
    /// </summary>
    public static IServiceCollection AddPartidas(this IServiceCollection servicios)
    {
        // Singleton: todas las peticiones deben ver las mismas partidas en curso.
        servicios.AddSingleton<IPartidaRepo, PartidaEnMemoriaRepositorio>();

        servicios.AddScoped<ConfigurarPartida>();
        servicios.AddScoped<UnirsePartida>();
        servicios.AddScoped<IniciarPartida>();
        servicios.AddScoped<ObtenerPartida>();
        servicios.AddScoped<ListarPartidasEnEspera>();
        servicios.AddScoped<EjecutarAccion>();

        return servicios;
    }
}
