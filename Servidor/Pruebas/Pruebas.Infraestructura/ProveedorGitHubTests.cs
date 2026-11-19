using System.Net;
using System.Text;
using Aplicacion.Puertos;
using Infraestructura.Persistencia;
using Infraestructura.Seguridad;
using Xunit;

namespace PruebasInfraestructura;

/// <summary>US-29: el adaptador de GitHub valida las respuestas del proveedor (CA-29.5) sin red real.</summary>
public sealed class ProveedorGitHubTests : IDisposable
{
    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "reactor-cuentas-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
    }

    private static ProveedorGitHub Proveedor(Func<HttpRequestMessage, (HttpStatusCode, string)> responder, string? clientId = "cliente-123") =>
        new(new HttpClient(new ManejadorFalso(responder)), new OpcionesGitHub(clientId));

    private static (HttpStatusCode, string) Ok(string json) => (HttpStatusCode.OK, json);

    /// <summary>GitHub autoriza y responde usuario y correos.</summary>
    private static (HttpStatusCode, string) GitHubQueAutoriza(HttpRequestMessage r, string correos) => r.RequestUri!.AbsoluteUri switch
    {
        ProveedorGitHub.UrlToken => Ok("""{"access_token":"gho_abc","token_type":"bearer","scope":"read:user,user:email"}"""),
        ProveedorGitHub.UrlUsuario => Ok("""{"id":12345,"login":"palma"}"""),
        ProveedorGitHub.UrlCorreos => Ok(correos),
        _ => (HttpStatusCode.NotFound, "{}")
    };

    [Fact]
    [Trait("CA", "29.1")]
    public async Task Iniciar_LeeElCodigoDeGitHub()
    {
        var proveedor = Proveedor(_ => Ok(
            """{"device_code":"dev-1","user_code":"ABCD-1234","verification_uri":"https://github.com/login/device","expires_in":899,"interval":5}"""));

        var codigo = await proveedor.IniciarAsync();

        Assert.NotNull(codigo);
        Assert.Equal("dev-1", codigo.DeviceCode);
        Assert.Equal("ABCD-1234", codigo.UserCode);
        Assert.Equal("https://github.com/login/device", codigo.VerificationUri);
        Assert.Equal(899, codigo.ExpiraEnSegundos);
        Assert.Equal(5, codigo.IntervaloSegundos);
    }

    [Theory]
    [Trait("CA", "29.5")]
    [InlineData("""{"error":"unauthorized_client"}""")]          // device flow no activado
    [InlineData("""{"device_code":"dev-1"}""")]                  // faltan campos
    [InlineData("no es json")]
    public async Task Iniciar_RespuestaInvalida_DevuelveNull(string respuesta)
    {
        Assert.Null(await Proveedor(_ => Ok(respuesta)).IniciarAsync());
    }

    [Theory]
    [Trait("CA", "29.5")]
    [InlineData("authorization_pending", EstadoAutorizacion.Pendiente)]
    [InlineData("slow_down", EstadoAutorizacion.MasLento)]
    [InlineData("access_denied", EstadoAutorizacion.Denegado)]
    [InlineData("expired_token", EstadoAutorizacion.Expirado)]
    [InlineData("incorrect_device_code", EstadoAutorizacion.Error)]
    public async Task Consultar_TraduceLaRespuestaDeGitHub(string error, EstadoAutorizacion esperado)
    {
        var resultado = await Proveedor(_ => Ok($$"""{"error":"{{error}}"}""")).ConsultarAsync("dev-1");

        Assert.Equal(esperado, resultado.Estado);
        Assert.Null(resultado.Identidad);
    }

    [Fact]
    [Trait("CA", "29.3")]
    [Trait("CA", "29.5")]
    public async Task Consultar_Autorizado_LeeUsuarioYCorreoPrincipalVerificado()
    {
        string? autorizacion = null;
        var proveedor = Proveedor(r =>
        {
            if (r.RequestUri!.AbsoluteUri == ProveedorGitHub.UrlUsuario)
                autorizacion = r.Headers.Authorization?.ToString();
            return GitHubQueAutoriza(r,
                """[{"email":"viejo@correo.com","primary":false,"verified":true},{"email":"palma@correo.com","primary":true,"verified":true}]""");
        });

        var resultado = await proveedor.ConsultarAsync("dev-1");

        Assert.Equal(EstadoAutorizacion.Autorizado, resultado.Estado);
        Assert.NotNull(resultado.Identidad);
        Assert.Equal("12345", resultado.Identidad.IdExterno);
        Assert.Equal("palma", resultado.Identidad.Login);
        Assert.Equal("palma@correo.com", resultado.Identidad.CorreoVerificado);
        Assert.Equal("Bearer gho_abc", autorizacion);   // el token solo se usa para leer la identidad
    }

    [Fact]
    [Trait("CA", "29.5")]
    public async Task Consultar_CorreosSinVerificar_IdentidadSinCorreo()
    {
        var proveedor = Proveedor(r => GitHubQueAutoriza(r, """[{"email":"palma@correo.com","primary":true,"verified":false}]"""));

        var resultado = await proveedor.ConsultarAsync("dev-1");

        Assert.Equal(EstadoAutorizacion.Autorizado, resultado.Estado);
        Assert.Null(resultado.Identidad!.CorreoVerificado);
    }

    [Fact]
    [Trait("CA", "29.5")]
    public async Task Consultar_UsuarioSinId_EsError()
    {
        var proveedor = Proveedor(r => r.RequestUri!.AbsoluteUri == ProveedorGitHub.UrlUsuario
            ? Ok("""{"login":"palma"}""")
            : GitHubQueAutoriza(r, "[]"));

        Assert.Equal(EstadoAutorizacion.Error, (await proveedor.ConsultarAsync("dev-1")).Estado);
    }

    [Fact]
    [Trait("CA", "29.4")]
    public async Task Consultar_GitHubFalla_EsErrorSinExcepcion()
    {
        var proveedor = Proveedor(_ => (HttpStatusCode.InternalServerError, "{}"));

        Assert.Equal(EstadoAutorizacion.Error, (await proveedor.ConsultarAsync("dev-1")).Estado);
    }

    [Fact]
    public async Task Consultar_EnviaElClientIdYElTipoDeDeviceFlow()
    {
        string? cuerpo = null;
        var proveedor = Proveedor(r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Ok("""{"error":"authorization_pending"}""");
        });

        await proveedor.ConsultarAsync("dev-1");

        Assert.Contains("client_id=cliente-123", cuerpo);
        Assert.Contains("device_code=dev-1", cuerpo);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", cuerpo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SinClientId_NoEstaConfigurado(string? clientId)
    {
        Assert.False(Proveedor(_ => Ok("{}"), clientId).EstaConfigurado);
    }

    // ---------- cuentas-externas.json ----------

    [Fact]
    [Trait("CA", "29.6")]
    public async Task CuentasExternas_VincularDosVeces_NoDuplicaYSobreviveAlReinicio()
    {
        var ruta = Path.Combine(carpeta, "cuentas-externas.json");
        var usuario = Guid.NewGuid();

        var repo = new JsonCuentaExternaRepositorio(new JsonFileStore<CuentaExternaRegistro>(ruta));
        await repo.VincularAsync("github", "12345", usuario);
        await repo.VincularAsync("github", "12345", usuario);

        var reabierto = new JsonFileStore<CuentaExternaRegistro>(ruta);   // "reiniciar" = reabrir el archivo
        Assert.Single(await reabierto.LeerAsync());
        Assert.Equal(usuario, await new JsonCuentaExternaRepositorio(reabierto).BuscarUsuarioAsync("github", "12345"));
        Assert.Null(await new JsonCuentaExternaRepositorio(reabierto).BuscarUsuarioAsync("github", "99999"));
    }

    private sealed class ManejadorFalso(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (estado, cuerpo) = responder(request);
            return Task.FromResult(new HttpResponseMessage(estado)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            });
        }
    }
}
