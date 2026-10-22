using Aplicacion.Puertos;
using Contratos.Auth;
using Dominio.Usuarios;

namespace Aplicacion.CasosDeUso;

/// <summary>
/// US-01 Registro, US-02 Verificación de correo, US-03 Login, US-04 Perfil, US-05 Recuperación.
/// Solo conoce puertos (interfaces); no sabe si los datos viven en JSON, ni cómo se envía el correo.
/// </summary>
public sealed class AutenticacionService(
    IUsuarioRepo usuarios,
    ITokenUsuarioRepo tokens,
    IPasswordHasher contrasenas,
    ITokenService jwt,
    IMailSender correo,
    IClock reloj,
    IAvatarRepo avatares)
{
    private const string MensajeContrasenaDebil =
        "La contraseña debe tener 8 caracteres e incluir mayúscula, minúscula, número y símbolo.";

    public async Task<UsuarioDto> RegistrarAsync(RegistroRequest request, CancellationToken ct = default)
    {
        var username = request.Username?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";

        if (username.Length is < 3 or > 30)
            throw new AuthException("USERNAME_INVALIDO", "El nombre de usuario debe tener entre 3 y 30 caracteres.");
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new AuthException("CORREO_INVALIDO", "El correo electrónico no tiene un formato válido.");
        if (request.Password != request.ConfirmPassword)
            throw new AuthException("CONTRASENAS_NO_COINCIDEN", "Las contraseñas no coinciden.");
        if (!EsContrasenaValida(request.Password))
            throw new AuthException("CONTRASENA_DEBIL", MensajeContrasenaDebil);
        if (await usuarios.ObtenerPorUsernameAsync(username, ct) is not null)
            throw new AuthException("USUARIO_EXISTE", "El nombre de usuario ya está registrado.");
        if (await usuarios.ObtenerPorCorreoAsync(email, ct) is not null)
            throw new AuthException("CORREO_EXISTE", "El correo electrónico ya está registrado.");

        var usuario = new Usuario(Guid.NewGuid(), username, email, contrasenas.Hash(request.Password));
        await usuarios.GuardarAsync(usuario, ct);

        await CrearYEnviarTokenAsync(usuario, TipoToken.EMAIL_VERIFY, TimeSpan.FromHours(24),
            "Verifica tu cuenta", "Tu código de verificación es", ct);
        return Mapear(usuario);
    }

    public async Task VerificarCorreoAsync(string codigo, CancellationToken ct = default)
    {
        var token = await tokens.ObtenerAsync(jwt.Hash(codigo ?? ""), TipoToken.EMAIL_VERIFY, ct);
        if (token is null || !token.EstaVigente(reloj.UtcNow))
            throw new AuthException("TOKEN_INVALIDO", "El código de verificación es inválido o expiró.");

        var usuario = await usuarios.ObtenerPorIdAsync(token.UserId, ct) ?? throw UsuarioNoEncontrado();
        usuario.MarcarVerificado();
        token.Usar(reloj.UtcNow);
        await usuarios.GuardarAsync(usuario, ct);
        await tokens.ActualizarAsync(token, ct);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var valor = request.UsernameOrEmail?.Trim() ?? "";
        var usuario = await usuarios.ObtenerPorUsernameAsync(valor, ct)
                      ?? await usuarios.ObtenerPorCorreoAsync(valor.ToLowerInvariant(), ct);

        if (usuario is null || !contrasenas.Verificar(request.Password ?? "", usuario.HashContrasena))
            throw new AuthException("CREDENCIALES_INVALIDAS", "Usuario/correo o contraseña incorrectos.");
        if (!usuario.Verificado)
            throw new AuthException("CORREO_NO_VERIFICADO", "Tu correo está pendiente de verificación.");

        return new LoginResponse(jwt.CrearJwt(usuario), Mapear(usuario));
    }

    public async Task SolicitarRecuperacionAsync(string email, CancellationToken ct = default)
    {
        // Misma respuesta exista o no la cuenta, para no revelar qué correos están registrados.
        var usuario = await usuarios.ObtenerPorCorreoAsync((email ?? "").Trim().ToLowerInvariant(), ct);
        if (usuario is not null)
            await CrearYEnviarTokenAsync(usuario, TipoToken.PASSWORD_RESET, TimeSpan.FromHours(1),
                "Recupera tu contraseña", "Tu código de recuperación es", ct);
    }

    public async Task CambiarContrasenaAsync(CambiarContrasenaRequest request, CancellationToken ct = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
            throw new AuthException("CONTRASENAS_NO_COINCIDEN", "Las contraseñas no coinciden.");
        if (!EsContrasenaValida(request.NewPassword))
            throw new AuthException("CONTRASENA_DEBIL", MensajeContrasenaDebil);

        var token = await tokens.ObtenerAsync(jwt.Hash(request.Token ?? ""), TipoToken.PASSWORD_RESET, ct);
        if (token is null || !token.EstaVigente(reloj.UtcNow))
            throw new AuthException("TOKEN_INVALIDO", "El código de recuperación es inválido o expiró.");

        var usuario = await usuarios.ObtenerPorIdAsync(token.UserId, ct) ?? throw UsuarioNoEncontrado();
        usuario.CambiarContrasena(contrasenas.Hash(request.NewPassword));
        token.Usar(reloj.UtcNow);
        await usuarios.GuardarAsync(usuario, ct);
        await tokens.ActualizarAsync(token, ct);
    }

    public async Task<UsuarioDto> PerfilAsync(Guid userId, CancellationToken ct = default) =>
        Mapear(await usuarios.ObtenerPorIdAsync(userId, ct) ?? throw UsuarioNoEncontrado());

    public async Task<IReadOnlyList<AvatarDto>> ListarAvataresAsync(CancellationToken ct = default) =>
        (await avatares.ListarAsync(ct)).Select(a => new AvatarDto(a.Id, a.Nombre, a.Color)).ToList();

    public async Task<UsuarioDto> ElegirAvatarAsync(Guid userId, Guid avatarId, CancellationToken ct = default)
    {
        if (await avatares.ObtenerPorIdAsync(avatarId, ct) is null)
            throw new AuthException("AVATAR_INVALIDO", "Debes seleccionar uno de los avatares predeterminados.");

        var usuario = await usuarios.ObtenerPorIdAsync(userId, ct) ?? throw UsuarioNoEncontrado();
        usuario.ElegirAvatar(avatarId);
        await usuarios.GuardarAsync(usuario, ct);
        return Mapear(usuario);
    }

    private async Task CrearYEnviarTokenAsync(Usuario usuario, TipoToken tipo, TimeSpan duracion,
        string asunto, string introduccion, CancellationToken ct)
    {
        var codigo = jwt.GenerarTokenSeguro();
        await tokens.GuardarAsync(new TokenUsuario(usuario.Id, tipo, jwt.Hash(codigo), reloj.UtcNow.Add(duracion)), ct);
        await correo.EnviarAsync(usuario.Correo, asunto, $"{introduccion}: {codigo}", ct);
    }

    private static AuthException UsuarioNoEncontrado() =>
        new("USUARIO_NO_ENCONTRADO", "No se encontró el usuario.");

    private static UsuarioDto Mapear(Usuario u) =>
        new(u.Id, u.Nombre, u.Correo, u.Verificado, u.AvatarId);

    private static bool EsContrasenaValida(string? p) =>
        p is { Length: >= 8 } && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c));
}
