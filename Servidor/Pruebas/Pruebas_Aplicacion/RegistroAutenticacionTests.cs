using Aplicacion.CasosDeUso;
using Dominio.Avatares;
using Dominio.Usuarios;
using Xunit;
using Aplicacion.Puertos;
using Contratos.Auth;
using Infraestructura.Persistencia;
using Infraestructura.Seguridad;

namespace PruebasAplicacion;

/// <summary>
/// US-01 a US-05 con persistencia REAL en archivos JSON (carpeta temporal), sin base de datos.
/// "Reiniciar" = crear de nuevo los almacenes sobre la misma carpeta.
/// </summary>
public sealed class Pruebas_Registro : IDisposable
{
    private const string Clave = "Clave#1234";

    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "reactor-pruebas-" + Guid.NewGuid().ToString("N"));
    private readonly CorreoCapturado correo = new();
    private readonly RelojFalso reloj = new();

    public void Dispose()
    {
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
    }

    /// <summary>Arma el servicio sobre los mismos archivos; llamarlo otra vez simula reiniciar el servidor.</summary>
    private AutenticacionService NuevoServicio()
    {
        var seguridad = new JwtTokenService(new JwtOpciones("pruebas", "pruebas", new string('s', 40)));
        return new AutenticacionService(
            new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json")),
            new JsonTokenUsuarioRepositorio(new JsonFileStore<TokenRegistro>(Path.Combine(carpeta, "tokens.json"))),
            new BCryptPasswordHasher(), seguridad, correo, reloj, new AvatarRepoFalso(),
            new ArchivoAvatarPropioRepositorio(carpeta));
    }

    private static RegistroRequest Registro(string usuario = "ana", string email = "ana@correo.com", string clave = Clave, string? confirmar = null) =>
        new(usuario, email, clave, confirmar ?? clave);

    private string UltimoCodigo() => correo.Enviados[^1].Cuerpo.Split(": ")[^1].Trim();

    private async Task<AutenticacionService> UsuarioVerificadoAsync()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());
        await servicio.VerificarCorreoAsync(UltimoCodigo());
        return servicio;
    }

    // ---------- US-01 Registro ----------

    [Fact]
    public async Task Registro_Valido_GuardaEnJsonYNoGuardaLaContrasenaEnClaro()
    {
        var usuario = await NuevoServicio().RegistrarAsync(Registro());

        Assert.False(usuario.EmailVerified);
        Assert.NotNull(usuario.AvatarId);   // nace con un avatar por defecto del catálogo
        var json = await File.ReadAllTextAsync(Path.Combine(carpeta, "usuarios.json"));
        Assert.Contains("\"nombre\": \"ana\"", json);
        Assert.DoesNotContain(Clave, json);
    }

    [Fact]
    public async Task Registro_EnviaElCodigoPorCorreoYSoloGuardaSuHash()
    {
        await NuevoServicio().RegistrarAsync(Registro());

        Assert.Single(correo.Enviados);
        Assert.Equal("ana@correo.com", correo.Enviados[0].Destinatario);
        var tokens = await File.ReadAllTextAsync(Path.Combine(carpeta, "tokens.json"));
        Assert.DoesNotContain($"\"{UltimoCodigo()}\"", tokens);
    }

    [Fact]
    public async Task Registro_ElCodigoDeVerificacionTiene6Digitos()
    {
        await NuevoServicio().RegistrarAsync(Registro());

        var codigo = UltimoCodigo();
        Assert.Matches("^[0-9]{6}$", codigo);
    }

    [Fact]
    public async Task Verificacion_CodigoVencidoALos30Minutos_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());
        var codigo = UltimoCodigo();
        reloj.Avanzar(TimeSpan.FromMinutes(31));

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.VerificarCorreoAsync(codigo));
        Assert.Equal("TOKEN_INVALIDO", error.Codigo);
    }

    [Fact]
    public async Task Registro_SiFallaElEnvioDelCorreo_NoDejaLaCuentaGuardada()
    {
        var servicio = NuevoServicio();
        correo.Falla = true;

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.RegistrarAsync(Registro()));
        Assert.Equal("CORREO_NO_ENVIADO", error.Codigo);

        correo.Falla = false;   // el SMTP vuelve: el mismo usuario y correo se pueden registrar
        await servicio.RegistrarAsync(Registro());
        Assert.Matches("^[0-9]{6}$", UltimoCodigo());
    }

    [Fact]
    public async Task Registro_UsuarioRepetidoSinDistinguirMayusculas_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.RegistrarAsync(Registro("ANA", "otra@correo.com")));

        Assert.Equal("USUARIO_EXISTE", error.Codigo);
    }

    [Fact]
    public async Task Registro_CorreoRepetido_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.RegistrarAsync(Registro("beto", "ANA@correo.com")));

        Assert.Equal("CORREO_EXISTE", error.Codigo);
    }

    [Theory]
    [InlineData("corta1!", "CONTRASENA_DEBIL")]
    [InlineData("sinnumeros!A", "CONTRASENA_DEBIL")]
    public async Task Registro_ContrasenaDebil_Falla(string clave, string codigo)
    {
        var error = await Assert.ThrowsAsync<AuthException>(() => NuevoServicio().RegistrarAsync(Registro(clave: clave)));

        Assert.Equal(codigo, error.Codigo);
    }

    [Fact]
    public async Task Registro_ConfirmacionDistinta_Falla()
    {
        var error = await Assert.ThrowsAsync<AuthException>(() => NuevoServicio().RegistrarAsync(Registro(confirmar: "Otra#1234")));

        Assert.Equal("CONTRASENAS_NO_COINCIDEN", error.Codigo);
    }

    [Fact]
    public async Task Registro_CorreoInvalido_Falla()
    {
        var error = await Assert.ThrowsAsync<AuthException>(() => NuevoServicio().RegistrarAsync(Registro(email: "no-es-correo")));

        Assert.Equal("CORREO_INVALIDO", error.Codigo);
    }

    // ---------- US-02 Verificación y persistencia entre reinicios ----------

    [Fact]
    public async Task Verificacion_CodigoValido_MarcaElCorreoYSobreviveAlReinicio()
    {
        var antes = NuevoServicio();
        var creado = await antes.RegistrarAsync(Registro());
        await antes.VerificarCorreoAsync(UltimoCodigo());

        var despuesDeReiniciar = NuevoServicio();   // relee usuarios.json desde el disco
        var perfil = await despuesDeReiniciar.PerfilAsync(creado.Id);

        Assert.True(perfil.EmailVerified);
    }

    [Fact]
    public async Task Verificacion_CodigoInvalido_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.VerificarCorreoAsync("codigo-inventado"));

        Assert.Equal("TOKEN_INVALIDO", error.Codigo);
    }

    [Fact]
    public async Task Verificacion_CodigoYaUsado_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());
        var codigo = UltimoCodigo();
        await servicio.VerificarCorreoAsync(codigo);

        await Assert.ThrowsAsync<AuthException>(() => NuevoServicio().VerificarCorreoAsync(codigo));
    }

    [Fact]
    public async Task Verificacion_CodigoVencido_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());
        reloj.Avanzar(TimeSpan.FromHours(25));

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.VerificarCorreoAsync(UltimoCodigo()));

        Assert.Equal("TOKEN_INVALIDO", error.Codigo);
    }

    // ---------- US-03 Login ----------

    [Fact]
    public async Task Login_ConUsuarioOCorreo_DespuesDeReiniciar_EntregaJwt()
    {
        await UsuarioVerificadoAsync();
        var reiniciado = NuevoServicio();

        var porUsuario = await reiniciado.LoginAsync(new LoginRequest("ANA", Clave));
        var porCorreo = await reiniciado.LoginAsync(new LoginRequest("Ana@Correo.com", Clave));

        Assert.False(string.IsNullOrWhiteSpace(porUsuario.AccessToken));
        Assert.Equal(porUsuario.User.Id, porCorreo.User.Id);
    }

    [Fact]
    public async Task Login_ContrasenaIncorrecta_Falla()
    {
        var servicio = await UsuarioVerificadoAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.LoginAsync(new LoginRequest("ana", "Incorrecta#1")));

        Assert.Equal("CREDENCIALES_INVALIDAS", error.Codigo);
    }

    [Fact]
    public async Task Login_SinVerificarElCorreo_Falla()
    {
        var servicio = NuevoServicio();
        await servicio.RegistrarAsync(Registro());

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.LoginAsync(new LoginRequest("ana", Clave)));

        Assert.Equal("CORREO_NO_VERIFICADO", error.Codigo);
    }

    [Fact]
    public async Task Login_CuentaSinAvatar_LeAsignaUnoPorDefectoYLoConserva()
    {
        // Cuenta creada antes de que existiera el avatar por defecto.
        var repo = new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json"));
        var viejo = new Usuario(Guid.NewGuid(), "viejo", "viejo@correo.com", new BCryptPasswordHasher().Hash(Clave));
        viejo.MarcarVerificado();
        await repo.GuardarAsync(viejo);

        var respuesta = await NuevoServicio().LoginAsync(new LoginRequest("viejo", Clave));

        Assert.NotNull(respuesta.User.AvatarId);
        Assert.Equal(respuesta.User.AvatarId, (await NuevoServicio().PerfilAsync(viejo.Id)).AvatarId);
    }

    // ---------- US-04 Perfil / avatar ----------

    [Fact]
    public async Task Avatar_Del_Catalogo_SeConservaTrasReiniciar()
    {
        var servicio = await UsuarioVerificadoAsync();
        var id = (await servicio.LoginAsync(new LoginRequest("ana", Clave))).User.Id;

        await servicio.ElegirAvatarAsync(id, AvatarRepoFalso.Cuarto);

        Assert.Equal(AvatarRepoFalso.Cuarto, (await NuevoServicio().PerfilAsync(id)).AvatarId);
    }

    [Fact]
    public async Task Avatar_FueraDelCatalogo_Falla()
    {
        var servicio = await UsuarioVerificadoAsync();
        var id = (await servicio.LoginAsync(new LoginRequest("ana", Clave))).User.Id;

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.ElegirAvatarAsync(id, Guid.NewGuid()));

        Assert.Equal("AVATAR_INVALIDO", error.Codigo);
    }

    [Fact]
    public async Task Avatar_Propio_SePuedeElegirSoloPorSuDueno()
    {
        var servicio = await UsuarioVerificadoAsync();
        var id = (await servicio.LoginAsync(new LoginRequest("ana", Clave))).User.Id;
        var propios = new ArchivoAvatarPropioRepositorio(carpeta);
        var subida = new AvatarPropioService(new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json")), propios);
        var avatarId = await subida.SubirAsync(id, PngMinimo());

        await servicio.ElegirAvatarAsync(id, AvatarRepoFalso.Cuarto);
        await servicio.ElegirAvatarAsync(id, avatarId);          // vuelve a su imagen propia
        Assert.Equal(avatarId, (await NuevoServicio().PerfilAsync(id)).AvatarId);

        var otro = await Assert.ThrowsAsync<AuthException>(() => servicio.ElegirAvatarAsync(Guid.NewGuid(), avatarId));
        Assert.Equal("AVATAR_INVALIDO", otro.Codigo);
    }

    private static byte[] PngMinimo() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    // ---------- US-05 Recuperación de contraseña ----------

    [Fact]
    public async Task Recuperacion_CodigoValido_CambiaLaContrasena()
    {
        var servicio = await UsuarioVerificadoAsync();
        await servicio.SolicitarRecuperacionAsync("ana@correo.com");

        await servicio.CambiarContrasenaAsync(new CambiarContrasenaRequest(UltimoCodigo(), "Nueva#5678", "Nueva#5678"));

        var reiniciado = NuevoServicio();
        Assert.False(string.IsNullOrWhiteSpace((await reiniciado.LoginAsync(new LoginRequest("ana", "Nueva#5678"))).AccessToken));
        await Assert.ThrowsAsync<AuthException>(() => reiniciado.LoginAsync(new LoginRequest("ana", Clave)));
    }

    [Fact]
    public async Task Recuperacion_CodigoVencido_Falla()
    {
        var servicio = await UsuarioVerificadoAsync();
        await servicio.SolicitarRecuperacionAsync("ana@correo.com");
        var codigo = UltimoCodigo();
        reloj.Avanzar(TimeSpan.FromHours(2));

        var error = await Assert.ThrowsAsync<AuthException>(() =>
            servicio.CambiarContrasenaAsync(new CambiarContrasenaRequest(codigo, "Nueva#5678", "Nueva#5678")));

        Assert.Equal("TOKEN_INVALIDO", error.Codigo);
    }

    [Fact]
    public async Task Recuperacion_CorreoDesconocido_NoEnviaNadaNiRevelaSiExiste()
    {
        var servicio = NuevoServicio();

        await servicio.SolicitarRecuperacionAsync("nadie@correo.com");

        Assert.Empty(correo.Enviados);
    }

    // ---------- Dispositivos (devices.json) ----------

    [Fact]
    public async Task Dispositivo_Asociado_SeEncuentraDespuesDeReiniciar_YUnJugadorTieneUnSoloControl()
    {
        var jugador1 = Guid.NewGuid();
        var jugador2 = Guid.NewGuid();
        IDispositivoRepo Repo() => new JsonDispositivoRepositorio(new JsonFileStore<DispositivoRegistro>(Path.Combine(carpeta, "dispositivos.json")), reloj);

        await Repo().AsignarAsync("pico-01", jugador1);
        await Repo().AsignarAsync("pico-02", jugador2);
        await Repo().AsignarAsync("pico-03", jugador1);   // jugador1 cambia de control

        Assert.Null(await Repo().ObtenerPorDeviceIdAsync("pico-01"));
        Assert.Equal(jugador1, (await Repo().ObtenerPorDeviceIdAsync("PICO-03"))!.UserId);
        Assert.Equal(jugador2, (await Repo().ObtenerPorDeviceIdAsync("pico-02"))!.UserId);
    }

    [Fact]
    public async Task Dispositivo_NoAsociado_NoSeEncuentra()
    {
        var repo = new JsonDispositivoRepositorio(new JsonFileStore<DispositivoRegistro>(Path.Combine(carpeta, "dispositivos.json")), reloj);

        Assert.Null(await repo.ObtenerPorDeviceIdAsync("pico-99"));
    }

    // ---------- JsonFileStore ----------

    [Fact]
    public async Task Almacen_ArchivoCorrupto_SeRecuperaDeLaCopiaBak()
    {
        var ruta = Path.Combine(carpeta, "usuarios.json");
        var servicio = NuevoServicio();
        var creado = await servicio.RegistrarAsync(Registro());
        await servicio.RegistrarAsync(Registro("beto", "beto@correo.com"));   // segunda escritura crea usuarios.json.bak
        await File.WriteAllTextAsync(ruta, "{ esto no es json");

        var recuperado = await NuevoServicio().PerfilAsync(creado.Id);

        Assert.Equal("ana", recuperado.Username);
    }

    // ---------- Apoyos de prueba ----------

    private sealed class AvatarRepoFalso : IAvatarRepo
    {
        public static readonly Guid Cuarto = Guid.Parse("00000000-0000-0000-0000-000000000004");
        private static readonly Avatar[] Lista =
            [new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Uno", "#FF0000"), new(Cuarto, "Cuatro", "#0000FF")];

        public Task<IReadOnlyList<Avatar>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Avatar>>(Lista);
        public Task<Avatar?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Lista.FirstOrDefault(a => a.Id == id));
    }

    private sealed class CorreoCapturado : IMailSender
    {
        public List<(string Destinatario, string Asunto, string Cuerpo)> Enviados { get; } = [];
        public bool Falla { get; set; }

        public Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
        {
            if (Falla) throw new InvalidOperationException("SMTP caído");
            Enviados.Add((destinatario, asunto, cuerpo));
            return Task.CompletedTask;
        }
    }

    private sealed class RelojFalso : IClock
    {
        private DateTime ahora = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => ahora;
        public void Avanzar(TimeSpan tiempo) => ahora += tiempo;
    }
}
