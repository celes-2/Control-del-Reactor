using System.Text.Json;
using Dominio.Juego;

namespace Infraestructura.Hardware;

/// <summary>
/// Convierte un mensaje MQTT del control en un EventoBoton.
/// Topic:   arena/devices/{deviceId}/events
/// Mensaje: {"button":"UP","seq":42}
/// Si el mensaje no es valido (sin button, boton desconocido o sin seq) devuelve null y el evento se descarta.
/// </summary>
public static class TraductorEventoBoton
{
    private const string PrefijoTopic = "arena/devices/";
    private const string SufijoTopic = "/events";
    private const string SufijoEstado = "/status";

    /// <summary>Topic al que se suscribe el servidor: el "+" acepta cualquier control.</summary>
    public const string TopicSuscripcion = "arena/devices/+/events";

    /// <summary>Topic de estado de los controles: "online" al conectarse, "offline" por Last Will.</summary>
    public const string TopicEstado = "arena/devices/+/status";

    public static EventoBoton? Traducir(string topic, string mensaje)
    {
        var deviceId = ExtraerDeviceId(topic);
        if (deviceId is null)
            return null;

        try
        {
            using var documento = JsonDocument.Parse(mensaje);
            var raiz = documento.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object)
                return null;

            if (!raiz.TryGetProperty("button", out var boton) || boton.ValueKind != JsonValueKind.String)
                return null;

            var accion = ConvertirBoton(boton.GetString());
            if (accion is null)
                return null;

            // Sin seq no se puede detectar un duplicado (CA-18.8): el mensaje se descarta.
            if (!raiz.TryGetProperty("seq", out var s)
                || s.ValueKind != JsonValueKind.Number
                || !s.TryGetInt32(out var seq))
                return null;

            return new EventoBoton(deviceId, accion.Value, seq);
        }
        catch (JsonException)
        {
            return null;   // el mensaje no era JSON
        }
    }

    /// <summary>Saca el deviceId de un topic de eventos, o null si el topic no tiene el formato esperado.</summary>
    public static string? ExtraerDeviceId(string topic) => ExtraerDeviceId(topic, SufijoTopic);

    /// <summary>True si el topic es el de estado de un control (arena/devices/{deviceId}/status).</summary>
    public static bool EsTopicEstado(string topic) => ExtraerDeviceId(topic, SufijoEstado) is not null;

    /// <summary>Si el mensaje es el "online" de un control devuelve su deviceId; si no, null.</summary>
    public static string? ControlEnLinea(string topic, string mensaje)
    {
        var deviceId = ExtraerDeviceId(topic, SufijoEstado);
        var esOnline = string.Equals(mensaje?.Trim(), "online", StringComparison.OrdinalIgnoreCase);
        return deviceId is not null && esOnline ? deviceId : null;
    }

    /// <summary>Saca el deviceId de "arena/devices/{deviceId}{sufijo}", o null si no tiene ese formato.</summary>
    private static string? ExtraerDeviceId(string topic, string sufijo)
    {
        if (string.IsNullOrEmpty(topic)
            || topic.Length <= PrefijoTopic.Length + sufijo.Length
            || !topic.StartsWith(PrefijoTopic, StringComparison.Ordinal)
            || !topic.EndsWith(sufijo, StringComparison.Ordinal))
            return null;

        var deviceId = topic[PrefijoTopic.Length..^sufijo.Length];
        return deviceId.Contains('/') ? null : deviceId;
    }

    /// <summary>UP, DOWN, LEFT, RIGHT, ACTION -> accion del juego. Cualquier otro texto -> null.</summary>
    public static Accion? ConvertirBoton(string? boton) => boton?.Trim().ToUpperInvariant() switch
    {
        "UP" => Accion.Arriba,
        "DOWN" => Accion.Abajo,
        "LEFT" => Accion.Izquierda,
        "RIGHT" => Accion.Derecha,
        "ACTION" => Accion.Extraer,
        _ => null
    };
}