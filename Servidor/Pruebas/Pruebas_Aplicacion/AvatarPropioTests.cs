using Aplicacion.CasosDeUso;
using Dominio.Usuarios;
using Infraestructura.Persistencia;
using Xunit;

namespace PruebasAplicacion;

/// <summary>CA-06.3 y CA-06.4: imagen propia como avatar, con archivos reales en una carpeta temporal.</summary>
public sealed class AvatarPropioTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "reactor-avatar-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
    }

    private async Task<(AvatarPropioService Servicio, Guid UserId)> ConUsuarioAsync()
    {
        var usuarios = new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json"));
        var usuario = new Usuario(Guid.NewGuid(), "ana", "ana@correo.com", "hash");
        await usuarios.GuardarAsync(usuario);
        return (new AvatarPropioService(usuarios, new ArchivoAvatarPropioRepositorio(carpeta)), usuario.Id);
    }

    [Fact]
    public async Task Subir_Png_QuedaComoAvatarYSeSirve()
    {
        var (servicio, userId) = await ConUsuarioAsync();

        var avatarId = await servicio.SubirAsync(userId, Png);

        var imagen = await servicio.ObtenerImagenAsync(avatarId);
        Assert.NotNull(imagen);
        Assert.Equal("image/png", imagen.TipoContenido);
        Assert.Equal(Png, imagen.Contenido);
        var recargado = await new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json")).ObtenerPorIdAsync(userId);
        Assert.Equal(avatarId, recargado!.AvatarId);
    }

    [Fact]
    public async Task Subir_Jpg_Funciona()
    {
        var (servicio, userId) = await ConUsuarioAsync();

        var avatarId = await servicio.SubirAsync(userId, Jpg);

        Assert.Equal("image/jpeg", (await servicio.ObtenerImagenAsync(avatarId))!.TipoContenido);
    }

    [Fact]
    public async Task Subir_SobrevivaAlReinicio()
    {
        var (servicio, userId) = await ConUsuarioAsync();
        var avatarId = await servicio.SubirAsync(userId, Png);

        var otraInstancia = new AvatarPropioService(
            new JsonUsuarioRepositorio(Path.Combine(carpeta, "usuarios.json")), new ArchivoAvatarPropioRepositorio(carpeta));

        Assert.NotNull(await otraInstancia.ObtenerImagenAsync(avatarId));
    }

    [Fact]
    public async Task Subir_DeNuevo_ReemplazaLaAnteriorYBorraSuArchivo()
    {
        var (servicio, userId) = await ConUsuarioAsync();
        var primero = await servicio.SubirAsync(userId, Png);

        var segundo = await servicio.SubirAsync(userId, Jpg);

        Assert.Null(await servicio.ObtenerImagenAsync(primero));
        Assert.NotNull(await servicio.ObtenerImagenAsync(segundo));
        Assert.Single(Directory.GetFiles(Path.Combine(carpeta, "avatares")));
    }

    [Fact]
    public async Task Subir_ArchivoQueNoEsImagen_Falla()
    {
        var (servicio, userId) = await ConUsuarioAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.SubirAsync(userId, "esto no es una imagen"u8.ToArray()));

        Assert.Equal("IMAGEN_NO_SOPORTADA", error.Codigo);
    }

    [Fact]
    public async Task Subir_Vacio_Falla()
    {
        var (servicio, userId) = await ConUsuarioAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.SubirAsync(userId, []));

        Assert.Equal("IMAGEN_VACIA", error.Codigo);
    }

    [Fact]
    public async Task Subir_MuyGrande_Falla()
    {
        var (servicio, userId) = await ConUsuarioAsync();
        var grande = new byte[AvatarPropioService.MaxBytes + 1];
        Png.CopyTo(grande, 0);

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.SubirAsync(userId, grande));

        Assert.Equal("IMAGEN_MUY_GRANDE", error.Codigo);
    }

    [Fact]
    public async Task Subir_UsuarioInexistente_Falla()
    {
        var (servicio, _) = await ConUsuarioAsync();

        var error = await Assert.ThrowsAsync<AuthException>(() => servicio.SubirAsync(Guid.NewGuid(), Png));

        Assert.Equal("USUARIO_NO_ENCONTRADO", error.Codigo);
    }

    [Fact]
    public async Task ObtenerImagen_DeUnAvatarPredeterminado_DevuelveNull()
    {
        var (servicio, _) = await ConUsuarioAsync();

        Assert.Null(await servicio.ObtenerImagenAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, null)]
    [InlineData(new byte[] { 0x89, 0x50 }, null)]
    public void DetectarTipo_ReconocePorLaFirma(byte[] datos, string? esperado) =>
        Assert.Equal(esperado, AvatarPropioService.DetectarTipo(datos));
}
