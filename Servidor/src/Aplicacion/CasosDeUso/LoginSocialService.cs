using System.Security.Cryptography;
using Aplicacion.Puertos;
using Contratos.Auth;
using Dominio.Usuarios;

namespace Aplicacion.CasosDeUso;

/// <summary>
/// US-29: inicio de sesion con GitHub (OAuth device flow).
/// El servidor habla con GitHub, recibe la identidad ya validada, la vincula a una cuenta interna
/// sin duplicados y entrega el MISMO JWT que el login normal.
/// Nunca guarda contrasenas de GitHub ni su token.
/// </summary>
public sealed class LoginSocialService(
    IProveedorGitHub github,
    ICuentaExternaRepo cuentas,
    IUsuarioRepo usuarios,
    IAvatarRepo avatares,
    IPasswordHasher contrasenas,
    ITokenService jwt)
{
    public const string Proveedor = "github";
    public const string EstadoPendiente = "PENDIENTE";
    public const string EstadoCompletado = "COMPLETADO";

    /// <summary>Si GitHub pide ir mas lento (slow_down), Unity espera esto antes de volver a preguntar.</summary>
    public const int SegundosSiVaMuyRapido = 10;

    public async Task<GitHubInicioResponse> IniciarAsync(CancellationToken ct = default)
    {
        AsegurarConfigurado();

        var codigo = await github.IniciarAsync(ct)
            ?? throw new AuthException("LOGIN_SOCIAL_FALLO", "No se pudo conectar con GitHub. Inténtalo de nuevo.");

        return new GitHubInicioResponse(
            codigo.DeviceCode, codigo.UserCode, codigo.VerificationUri, codigo.ExpiraEnSegundos, codigo.IntervaloSegundos);
    }

    public async Task<GitHubConsultaResponse> ConsultarAsync(string? deviceCode, CancellationToken ct = default)
    {
        AsegurarConfigurado();
        if (string.IsNullOrWhiteSpace(deviceCode))
            throw new AuthException("LOGIN_SOCIAL_FALLO", "Falta el código del dispositivo.");

        var resultado = await github.ConsultarAsync(deviceCode.Trim(), ct);

        switch (resultado.Estado)
        {
            case EstadoAutorizacion.Pendiente:
                return new GitHubConsultaResponse(EstadoPendiente, null, null);

            case EstadoAutorizacion.MasLento:
                return new GitHubConsultaResponse(EstadoPendiente, SegundosSiVaMuyRapido, null);

            case EstadoAutorizacion.Autorizado when resultado.Identidad is { } identidad:
                return new GitHubConsultaResponse(EstadoCompletado, null, await IniciarSesionAsync(identidad, ct));

            // CA-29.4: rechazo, cancelacion o fallo informan el motivo y NO crean sesion.
            case EstadoAutorizacion.Denegado:
                throw new AuthException("LOGIN_SOCIAL_CANCELADO", "Cancelaste el inicio de sesión con GitHub.");

            case EstadoAutorizacion.Expirado:
                throw new AuthException("LOGIN_SOCIAL_EXPIRADO", "El código de GitHub expiró. Vuelve a intentarlo.");

            default:
                throw new AuthException("LOGIN_SOCIAL_FALLO", "No se pudo iniciar sesión con GitHub. Inténtalo de nuevo.");
        }
    }

    private async Task<LoginResponse> IniciarSesionAsync(IdentidadExterna identidad, CancellationToken ct)
    {
        var usuario = await BuscarOCrearAsync(identidad, ct);
        return new LoginResponse(
            jwt.CrearJwt(usuario),
            new UsuarioDto(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Verificado, usuario.AvatarId));
    }

    /// <summary>
    /// CA-29.6, una sola cuenta por persona:
    /// 1) la cuenta de GitHub ya estaba vinculada: se usa esa cuenta;
    /// 2) existe una cuenta interna con el mismo correo: se vincula a ella. Si esa cuenta NO estaba
    ///    verificada, se anula su contraseña (pudo crearla otra persona con este correo) - defecto D-09;
    /// 3) si no, se crea una cuenta nueva y se vincula.
    /// </summary>
    private async Task<Usuario> BuscarOCrearAsync(IdentidadExterna identidad, CancellationToken ct)
    {
        if (await cuentas.BuscarUsuarioAsync(Proveedor, identidad.IdExterno, ct) is { } vinculadoId
            && await usuarios.ObtenerPorIdAsync(vinculadoId, ct) is { } vinculado)
            return vinculado;

        var correo = identidad.CorreoVerificado?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(correo))
            throw new AuthException("LOGIN_SOCIAL_SIN_CORREO",
                "Tu cuenta de GitHub no tiene un correo verificado. Verifica uno en GitHub o regístrate con correo y contraseña.");

        var usuario = await usuarios.ObtenerPorCorreoAsync(correo, ct);
        if (usuario is null)
        {
            // Contraseña aleatoria que nadie conoce: esta cuenta entra con GitHub
            // (o con "olvidé mi contraseña" si algún día quiere una).
            usuario = new Usuario(Guid.NewGuid(), await NombreLibreAsync(identidad.Login, ct), correo,
                contrasenas.Hash(jwt.GenerarTokenSeguro()));
            await AsignarAvatarPorDefectoAsync(usuario, ct);
        }
        else if (!usuario.Verificado)
        {
            // D-09 (revisión de Faby): nadie probó que este correo fuera suyo, así que la cuenta pudo
            // crearla otra persona. GitHub sí lo prueba para quien entra ahora: se anula la contraseña
            // anterior para que quien la creó no conserve el acceso. Puede crear una nueva con
            // "olvidé mi contraseña".
            usuario.CambiarContrasena(contrasenas.Hash(jwt.GenerarTokenSeguro()));
        }

        // GitHub ya verificó ese correo, así que la cuenta puede jugar (CA-29.7: mismas reglas que el login normal).
        usuario.MarcarVerificado();
        await usuarios.GuardarAsync(usuario, ct);
        await cuentas.VincularAsync(Proveedor, identidad.IdExterno, usuario.Id, ct);
        return usuario;
    }

    /// <summary>El nombre de GitHub si está libre; si no, "nombre-gh", "nombre-gh2", ...</summary>
    private async Task<string> NombreLibreAsync(string login, CancellationToken ct)
    {
        var baseNombre = string.IsNullOrWhiteSpace(login) ? "jugador" : login.Trim();
        if (baseNombre.Length > 24) baseNombre = baseNombre[..24];
        if (baseNombre.Length < 3) baseNombre += "-gh";

        var candidato = baseNombre;
        for (var intento = 1; intento <= 50; intento++)
        {
            if (await usuarios.ObtenerPorUsernameAsync(candidato, ct) is null)
                return candidato;
            candidato = intento == 1 ? $"{baseNombre}-gh" : $"{baseNombre}-gh{intento}";
        }
        return $"{baseNombre}-{Guid.NewGuid():N}"[..30];
    }

    /// <summary>Todo jugador nace con un avatar (US-06), igual que en el registro normal.</summary>
    private async Task AsignarAvatarPorDefectoAsync(Usuario usuario, CancellationToken ct)
    {
        var catalogo = await avatares.ListarAsync(ct);
        if (catalogo.Count > 0)
            usuario.ElegirAvatar(catalogo[RandomNumberGenerator.GetInt32(catalogo.Count)].Id);
    }

    private void AsegurarConfigurado()
    {
        if (!github.EstaConfigurado)
            throw new AuthException("LOGIN_SOCIAL_NO_CONFIGURADO",
                "El inicio de sesión con GitHub no está configurado en el servidor.");
    }
}