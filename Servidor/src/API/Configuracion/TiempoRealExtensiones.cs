using API.TiempoReal;
using Infraestructura.Hardware;

namespace API.Configuracion;

/// <summary>Registra todo lo del modulo de tiempo real (regla: cada modulo tiene su propio metodo).</summary>
public static class TiempoRealExtensiones
{
    public static IServiceCollection AddTiempoReal(this IServiceCollection services, IConfiguration configuration)
    {
        var opciones = configuration.GetSection("Mqtt").Get<OpcionesMqtt>() ?? new OpcionesMqtt();

        services.AddSingleton(opciones);
        services.AddSingleton<IFuenteEventosBoton, SuscriptorMqtt>();
        services.AddSingleton<AdministradorSalas>();
        services.AddSingleton<IEmparejamientoControles, EmparejamientoPorOrden>();
        services.AddSingleton<PublicadorPartida>();
        services.AddSingleton<ManejadorWebSocket>();
        services.AddHostedService<ServicioControles>();

        return services;
    }

    /// <summary>Activa los WebSockets y la ruta /ws/partida.</summary>
    public static WebApplication UseTiempoReal(this WebApplication app)
    {
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
        app.Map("/ws/partida", contexto =>
            contexto.RequestServices.GetRequiredService<ManejadorWebSocket>().AtenderAsync(contexto));
        return app;
    }
}