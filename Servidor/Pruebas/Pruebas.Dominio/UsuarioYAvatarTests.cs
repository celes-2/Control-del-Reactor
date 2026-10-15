using Dominio.Avatares;
using Dominio.Comun;
using Dominio.Usuarios;
using Xunit;

namespace PruebasDominio;

public class UsuarioYAvatarTests
{
    private static Usuario CrearUsuario() =>
        new(Guid.NewGuid(), "Ana", "Ana@Correo.com", "hash-de-prueba");

    [Fact]
    public void Usuario_NuevoNoEstaVerificadoYNormalizaElCorreo()
    {
        var usuario = CrearUsuario();

        Assert.False(usuario.Verificado);
        Assert.Equal("ana@correo.com", usuario.Correo);
    }

    [Fact]
    public void Usuario_MarcarVerificado_CambiaElEstado()
    {
        var usuario = CrearUsuario();

        usuario.MarcarVerificado();

        Assert.True(usuario.Verificado);
    }

    [Fact]
    public void Usuario_ElegirAvatar_GuardaElAvatar()
    {
        var usuario = CrearUsuario();
        var avatar = Guid.NewGuid();

        usuario.ElegirAvatar(avatar);

        Assert.Equal(avatar, usuario.AvatarId);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("")]
    public void Usuario_ConCorreoInvalido_Falla(string correo)
    {
        Assert.Throws<DominioException>(() =>
            new Usuario(Guid.NewGuid(), "Ana", correo, "hash"));
    }

    [Fact]
    public void Avatar_SinNombre_Falla()
    {
        Assert.Throws<DominioException>(() => new Avatar(Guid.NewGuid(), " ", "#FF8800"));
    }
}
