using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Dominio.Avatares;
using Dominio.Usuarios;
using Infraestructura.Seguridad;
using Xunit;

namespace PruebasAplicacion;

/// <summary>US-29: login con GitHub (device flow) con GitHub falso y repositorios en memoria.</summary>
public sealed class LoginSocialTests
{
    private static readonly Guid AvatarPorDefecto = Guid.NewGuid();

    private readonly GitHubFalso github = new();
    private readonly CuentasEnMemoria cuentas = new();
    private readonly UsuariosEnMemoria usuarios = new();

    private LoginSocialService Servicio() => new(
        github, cuentas, usuarios, new AvataresFalsos(), new HashFalso(),
        new JwtTokenService(new JwtOpciones("pruebas", "pruebas", new string('s', 48))));

    private static IdentidadExterna Palma(string correo = "palma@correo.com", string id = "12345") =>
        new(id, "palma", correo);

    // ---------- Positivas ----------

    [Fact]
    [Trait("CA", "29.1")]
    public async Task Iniciar_DevuelveElCodigoQueUnityMuestra()
    {
        var inicio = await Servicio().IniciarAsync();

        Assert.Equal("ABCD-1234", inicio.UserCode);
        Assert.Equal("https://github.com/login/device", inicio.VerificationUri);
        Assert.False(string.IsNullOrWhiteSpace(inicio.DeviceCode));
    }

    [Fact]
    public async Task Consultar_MientrasNoAutoriza_SiguePendienteSinSesion()
    {
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Pendiente);

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Equal(LoginSocialService.EstadoPendiente, r.Estado);
        Assert.Null(r.Login);
        Assert.Empty(usuarios.Todos);
    }

    [Fact]
    public async Task Consultar_SiGitHubPideIrMasLento_AvisaCuantoEsperar()
    {
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.MasLento);

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Equal(LoginSocialService.EstadoPendiente, r.Estado);
        Assert.Equal(LoginSocialService.SegundosSiVaMuyRapido, r.Interval);
    }

    [Fact]
    [Trait("CA", "29.3")]
    [Trait("CA", "29.7")]
    public async Task Autorizado_PrimeraVez_CreaCuentaVerificadaConAvatarYEntregaJwt()
    {
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, Palma());

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Equal(LoginSocialService.EstadoCompletado, r.Estado);
        Assert.NotNull(r.Login);
        Assert.False(string.IsNullOrWhiteSpace(r.Login.AccessToken));
        Assert.Equal("palma", r.Login.User.Username);
        Assert.Equal("palma@correo.com", r.Login.User.Email);
        Assert.True(r.Login.User.EmailVerified);              // puede jugar: mismas reglas que el login normal
        Assert.Equal(AvatarPorDefecto, r.Login.User.AvatarId);
        Assert.Equal(r.Login.User.Id, await cuentas.BuscarUsuarioAsync(LoginSocialService.Proveedor, "12345"));
    }

    [Fact]
    [Trait("CA", "29.6")]
    public async Task MismoUsuarioDeGitHubDosVeces_UnaSolaCuenta()
    {
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, Palma());
        var servicio = Servicio();

        var primera = await servicio.ConsultarAsync("dev-1");
        var segunda = await servicio.ConsultarAsync("dev-2");

        Assert.Single(usuarios.Todos);
        Assert.Equal(primera.Login!.User.Id, segunda.Login!.User.Id);
    }

    [Fact]
    [Trait("CA", "29.6")]
    public async Task CorreoYaRegistradoYVerificado_SeVinculaALaCuentaExistenteSinDuplicar()
    {
        var existente = new Usuario(Guid.NewGuid(), "palmita", "palma@correo.com", "hash-real");
        existente.MarcarVerificado();                      // la dueña ya probó que el correo es suyo
        await usuarios.GuardarAsync(existente);
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, Palma());

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Single(usuarios.Todos);
        Assert.Equal(existente.Id, r.Login!.User.Id);
        Assert.Equal("palmita", r.Login.User.Username);              // conserva su nombre
        Assert.Equal("hash-real", usuarios.Todos[0].HashContrasena); // y su contraseña
    }

    [Fact]
    [Trait("CA", "29.6")]
    public async Task CorreoRegistradoSinVerificar_SeVinculaYAnulaLaContrasenaAnterior()
    {
        // D-09: otra persona registró el correo de Palma con su propia contraseña y nunca pudo verificarlo.
        var impostor = new Usuario(Guid.NewGuid(), "impostor", "palma@correo.com", "hash:clave-del-impostor");
        await usuarios.GuardarAsync(impostor);
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, Palma());

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Single(usuarios.Todos);                                // no duplica la cuenta
        Assert.Equal(impostor.Id, r.Login!.User.Id);
        Assert.True(usuarios.Todos[0].Verificado);
        Assert.False(new HashFalso().Verificar("clave-del-impostor", usuarios.Todos[0].HashContrasena)); // ya no entra
    }

    [Fact]
    [Trait("CA", "29.6")]
    public async Task NombreDeGitHubOcupado_UsaUnNombreAlternativo()
    {
        await usuarios.GuardarAsync(new Usuario(Guid.NewGuid(), "palma", "otra@correo.com", "hash"));
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, Palma());

        var r = await Servicio().ConsultarAsync("dev-1");

        Assert.Equal(2, usuarios.Todos.Count);
        Assert.Equal("palma-gh", r.Login!.User.Username);
    }

    // ---------- Negativas ----------

    [Theory]
    [Trait("CA", "29.4")]
    [InlineData(EstadoAutorizacion.Denegado, "LOGIN_SOCIAL_CANCELADO")]
    [InlineData(EstadoAutorizacion.Expirado, "LOGIN_SOCIAL_EXPIRADO")]
    [InlineData(EstadoAutorizacion.Error, "LOGIN_SOCIAL_FALLO")]
    public async Task RechazoCancelacionOFallo_InformaYNoCreaSesion(EstadoAutorizacion estado, string codigo)
    {
        github.Resultado = new ResultadoAutorizacion(estado);

        var error = await Assert.ThrowsAsync<AuthException>(() => Servicio().ConsultarAsync("dev-1"));

        Assert.Equal(codigo, error.Codigo);
        Assert.Empty(usuarios.Todos);
        Assert.Null(await cuentas.BuscarUsuarioAsync(LoginSocialService.Proveedor, "12345"));
    }

    [Fact]
    [Trait("CA", "29.4")]
    [Trait("CA", "29.5")]
    public async Task GitHubSinCorreoVerificado_NoCreaCuenta()
    {
        github.Resultado = new ResultadoAutorizacion(EstadoAutorizacion.Autorizado, new IdentidadExterna("12345", "palma", null));

        var error = await Assert.ThrowsAsync<AuthException>(() => Servicio().ConsultarAsync("dev-1"));

        Assert.Equal("LOGIN_SOCIAL_SIN_CORREO", error.Codigo);
        Assert.Empty(usuarios.Todos);
    }

    [Fact]
    [Trait("CA", "29.4")]
    public async Task SinConfigurar_FallaConMensajeClaro()
    {
        github.Configurado = false;

        var error = await Assert.ThrowsAsync<AuthException>(() => Servicio().IniciarAsync());

        Assert.Equal("LOGIN_SOCIAL_NO_CONFIGURADO", error.Codigo);
    }

    [Fact]
    [Trait("CA", "29.4")]
    public async Task GitHubNoResponde_AlIniciar_Falla()
    {
        github.Codigo = null;

        var error = await Assert.ThrowsAsync<AuthException>(() => Servicio().IniciarAsync());

        Assert.Equal("LOGIN_SOCIAL_FALLO", error.Codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConsultarSinCodigo_Falla(string? deviceCode)
    {
        var error = await Assert.ThrowsAsync<AuthException>(() => Servicio().ConsultarAsync(deviceCode));

        Assert.Equal("LOGIN_SOCIAL_FALLO", error.Codigo);
    }

    // ---------- Dobles de prueba ----------

    private sealed class GitHubFalso : IProveedorGitHub
    {
        public bool Configurado { get; set; } = true;
        public CodigoDispositivo? Codigo { get; set; } =
            new("dev-1", "ABCD-1234", "https://github.com/login/device", 900, 5);
        public ResultadoAutorizacion Resultado { get; set; } = new(EstadoAutorizacion.Pendiente);

        public bool EstaConfigurado => Configurado;
        public Task<CodigoDispositivo?> IniciarAsync(CancellationToken ct = default) => Task.FromResult(Codigo);
        public Task<ResultadoAutorizacion> ConsultarAsync(string deviceCode, CancellationToken ct = default) =>
            Task.FromResult(Resultado);
    }

    private sealed class CuentasEnMemoria : ICuentaExternaRepo
    {
        private readonly Dictionary<string, Guid> vinculos = new();

        public Task<Guid?> BuscarUsuarioAsync(string proveedor, string idExterno, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(vinculos.TryGetValue($"{proveedor}:{idExterno}", out var id) ? id : null);

        public Task VincularAsync(string proveedor, string idExterno, Guid usuarioId, CancellationToken ct = default)
        {
            vinculos[$"{proveedor}:{idExterno}"] = usuarioId;
            return Task.CompletedTask;
        }
    }

    private sealed class UsuariosEnMemoria : IUsuarioRepo
    {
        public List<Usuario> Todos { get; } = [];

        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Todos.FirstOrDefault(u => u.Id == id));

        public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult(Todos.FirstOrDefault(u => string.Equals(u.Nombre, username, StringComparison.OrdinalIgnoreCase)));

        public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default) =>
            Task.FromResult(Todos.FirstOrDefault(u => string.Equals(u.Correo, correo, StringComparison.OrdinalIgnoreCase)));

        public Task GuardarAsync(Usuario usuario, CancellationToken ct = default)
        {
            Todos.RemoveAll(u => u.Id == usuario.Id);
            Todos.Add(usuario);
            return Task.CompletedTask;
        }
    }

    private sealed class AvataresFalsos : IAvatarRepo
    {
        private readonly Avatar avatar = new(AvatarPorDefecto, "Pablo", "#FF8800");

        public Task<IReadOnlyList<Avatar>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Avatar>>([avatar]);

        public Task<Avatar?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Avatar?>(id == avatar.Id ? avatar : null);
    }

    private sealed class HashFalso : IPasswordHasher
    {
        public string Hash(string contrasena) => "hash:" + contrasena;
        public bool Verificar(string contrasena, string hash) => hash == "hash:" + contrasena;
    }
}