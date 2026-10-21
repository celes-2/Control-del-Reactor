namespace Infraestructura.Hardware;

/// <summary>
/// Datos de conexion al broker. Se leen de appsettings.json (seccion "Mqtt")
/// o de variables de entorno (por ejemplo Mqtt__Host=mosquitto en Docker).
/// </summary>
public sealed class OpcionesMqtt
{
    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 1883;
    public string ClientId { get; set; } = "servidor-reactor";
}