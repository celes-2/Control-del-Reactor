namespace Infraestructura.Hardware;

/// <summary>
/// Datos de conexion al broker. Se leen de la seccion "Mqtt" de appsettings
/// o de variables de entorno, con los mismos nombres del docker-compose:
/// Mqtt__Host, Mqtt__Port, Mqtt__Username, Mqtt__Password.
/// </summary>
public sealed class OpcionesMqtt
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "servidor-reactor";

    /// <summary>Usuario y clave del broker. Si van vacios, se conecta anonimo.</summary>
    public string? Username { get; set; }
    public string? Password { get; set; }
}