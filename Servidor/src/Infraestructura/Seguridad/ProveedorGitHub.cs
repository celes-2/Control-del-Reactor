using System.Net.Http.Headers;
using System.Text.Json;
using Aplicacion.Puertos;

namespace Infraestructura.Seguridad;

/// <summary>ClientId de la OAuth App de GitHub (con Device Flow activado). Viene de .env (GITHUB_CLIENT_ID).</summary>
public sealed record OpcionesGitHub(string? ClientId);

/// <summary>
/// Adaptador del puerto <see cref="IProveedorGitHub"/>: OAuth device flow de GitHub.
/// Valida cada respuesta del proveedor (CA-29.5): si falta un campo, el tipo no es el esperado
/// o GitHub falla, se trata como error y nunca se crea una sesión.
/// El token de GitHub solo se usa para leer el usuario y el correo; no se guarda.
/// </summary>
public sealed class ProveedorGitHub(HttpClient http, OpcionesGitHub opciones) : IProveedorGitHub
{
    public const string UrlCodigo = "https://github.com/login/device/code";
    public const string UrlToken = "https://github.com/login/oauth/access_token";
    public const string UrlUsuario = "https://api.github.com/user";
    public const string UrlCorreos = "https://api.github.com/user/emails";
    private const string Alcance = "read:user user:email";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(opciones.ClientId);

    public async Task<CodigoDispositivo?> IniciarAsync(CancellationToken ct = default)
    {
        try
        {
            using var documento = await PostFormularioAsync(UrlCodigo, new Dictionary<string, string>
            {
                ["client_id"] = opciones.ClientId ?? "",
                ["scope"] = Alcance
            }, ct);

            var r = documento.RootElement;
            var deviceCode = Texto(r, "device_code");
            var userCode = Texto(r, "user_code");
            var uri = Texto(r, "verification_uri");
            if (deviceCode is null || userCode is null || uri is null)
                return null;

            return new CodigoDispositivo(deviceCode, userCode, uri, Entero(r, "expires_in") ?? 900, Entero(r, "interval") ?? 5);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<ResultadoAutorizacion> ConsultarAsync(string deviceCode, CancellationToken ct = default)
    {
        try
        {
            string? token;
            using (var documento = await PostFormularioAsync(UrlToken, new Dictionary<string, string>
                   {
                       ["client_id"] = opciones.ClientId ?? "",
                       ["device_code"] = deviceCode,
                       ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                   }, ct))
            {
                var r = documento.RootElement;
                switch (Texto(r, "error"))
                {
                    case "authorization_pending": return new ResultadoAutorizacion(EstadoAutorizacion.Pendiente);
                    case "slow_down": return new ResultadoAutorizacion(EstadoAutorizacion.MasLento);
                    case "access_denied": return new ResultadoAutorizacion(EstadoAutorizacion.Denegado);
                    case "expired_token": return new ResultadoAutorizacion(EstadoAutorizacion.Expirado);
                    case not null: return new ResultadoAutorizacion(EstadoAutorizacion.Error);
                }
                token = Texto(r, "access_token");
            }

            if (token is null)
                return new ResultadoAutorizacion(EstadoAutorizacion.Error);

            var identidad = await LeerIdentidadAsync(token, ct);
            return identidad is null
                ? new ResultadoAutorizacion(EstadoAutorizacion.Error)
                : new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, identidad);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new ResultadoAutorizacion(EstadoAutorizacion.Error);
        }
    }

    /// <summary>Lee el id y el nombre de usuario, y elige el correo verificado. Null si la respuesta no es válida.</summary>
    private async Task<IdentidadExterna?> LeerIdentidadAsync(string token, CancellationToken ct)
    {
        using var usuario = await GetAsync(UrlUsuario, token, ct);
        var u = usuario.RootElement;
        if (u.ValueKind != JsonValueKind.Object
            || !u.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number
            || !id.TryGetInt64(out var idNumero))
            return null;

        var login = Texto(u, "login");
        if (login is null)
            return null;

        using var correos = await GetAsync(UrlCorreos, token, ct);
        return new IdentidadExterna(idNumero.ToString(), login, ElegirCorreo(correos.RootElement));
    }

    /// <summary>El correo principal verificado; si no, el primero verificado; si no hay ninguno, null.</summary>
    public static string? ElegirCorreo(JsonElement lista)
    {
        if (lista.ValueKind != JsonValueKind.Array)
            return null;

        string? primerVerificado = null;
        foreach (var c in lista.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object
                || !c.TryGetProperty("verified", out var verificado) || verificado.ValueKind != JsonValueKind.True)
                continue;

            var correo = Texto(c, "email");
            if (correo is null)
                continue;

            if (c.TryGetProperty("primary", out var principal) && principal.ValueKind == JsonValueKind.True)
                return correo;

            primerVerificado ??= correo;
        }
        return primerVerificado;
    }

    private Task<JsonDocument> PostFormularioAsync(string url, Dictionary<string, string> campos, CancellationToken ct)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(campos) };
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return EnviarAsync(pedido, ct);
    }

    private Task<JsonDocument> GetAsync(string url, string token, CancellationToken ct)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return EnviarAsync(pedido, ct);
    }

    private async Task<JsonDocument> EnviarAsync(HttpRequestMessage pedido, CancellationToken ct)
    {
        using (pedido)
        {
            pedido.Headers.UserAgent.ParseAdd("ControlDelReactor/1.0");   // la API de GitHub exige User-Agent
            using var respuesta = await http.SendAsync(pedido, ct);
            respuesta.EnsureSuccessStatusCode();
            await using var flujo = await respuesta.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(flujo, cancellationToken: ct);
        }
    }

    private static string? Texto(JsonElement e, string nombre) =>
        e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(nombre, out var v)
        && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    private static int? Entero(JsonElement e, string nombre) =>
        e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(nombre, out var v)
        && v.ValueKind == JsonValueKind.Number
        && v.TryGetInt32(out var n)
            ? n
            : null;
}
