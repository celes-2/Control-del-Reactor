using System.Text.Json;
using Dominio.Juego;

namespace Infraestructura.Hardware;

/// <summary>
/// Convierte un mensaje MQTT del control en un EventoBoton.
/// Topic:   arena/devices/{deviceId}/events
/// Mensaje: {"button":"UP","seq":42}
/// Si el mensaje no es valido devuelve null y el evento se descarta.
/// </summary>
public static class TraductorEventoBoton
{
    private const string PrefijoTopic = "arena/devices/";
    private const string SufijoTopic = "/events";

    /// <summary>Topic al que se suscribe el servidor: el "+" acepta cualquier control.</summary>
    public const string TopicSuscripcion = "arena/devices/+/events";

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

            var seq = raiz.TryGetProperty("seq", out var s)
                      && s.ValueKind == JsonValueKind.Number
                      && s.TryGetInt32(out var numero)
                ? numero
                : 0;

            return new EventoBoton(deviceId, accion.Value, seq);
        }
        catch (JsonException)
        {
            return null;   // el mensaje no era JSON
        }
    }

    /// <summary>Saca el deviceId del topic, o null si el topic no tiene el formato esperado.</summary>
    public static string? ExtraerDeviceId(string topic)
    {
        if (string.IsNullOrEmpty(topic)
            || topic.Length <= PrefijoTopic.Length + SufijoTopic.Length
            || !topic.StartsWith(PrefijoTopic, StringComparison.Ordinal)
            || !topic.EndsWith(SufijoTopic, StringComparison.Ordinal))
            return null;

        var deviceId = topic[PrefijoTopic.Length..^SufijoTopic.Length];
        return deviceId.Contains('/') ? null : deviceId;
    }

    /// <summary>UP, DOWN, LEFT, RIGHT, ACTION -> accion del juego. Cualquier otro texto -> null.</summary>
    public static Accion? ConvertirBoton(string? boton) => boton?.Trim().ToUpperInvariant() switch
    {
        "UP" => Accion.Arriba,
        "DOWN" => Accion.Abajo,
        "LEFT" => Accion.Izquierda,
        "RIGHT" => Accion.Derecha,
        "ACTION" => Accion.Especial,
        _ => null
    };
}