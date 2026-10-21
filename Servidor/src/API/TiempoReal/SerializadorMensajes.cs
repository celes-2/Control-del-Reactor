using System.Text.Json;

namespace API.TiempoReal;

/// <summary>
/// Convierte los mensajes al formato del contrato: { "type": "...", "data": { ... } }
/// Los nombres salen en camelCase (UserId -> "userId").
/// </summary>
public static class SerializadorMensajes
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Serializar<T>(string tipo, T data) =>
        JsonSerializer.Serialize(new Sobre<T>(tipo, data), Opciones);

    /// <summary>Lee un mensaje de Unity. Devuelve null si no tiene el formato del contrato.</summary>
    public static (string Tipo, JsonElement Data)? Leer(string json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object)
                return null;
            if (!raiz.TryGetProperty("type", out var tipo) || tipo.ValueKind != JsonValueKind.String)
                return null;

            var data = raiz.TryGetProperty("data", out var d) ? d.Clone() : default;
            return (tipo.GetString()!, data);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Sobre<T>(string Type, T Data);
}
