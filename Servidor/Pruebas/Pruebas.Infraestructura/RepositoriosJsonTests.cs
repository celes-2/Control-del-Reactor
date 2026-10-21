using Dominio.Comun;
using Dominio.Usuarios;
using Infraestructura.Persistencia;
using Xunit;

namespace PruebasInfraestructura;

public sealed class RepositoriosJsonTests : IDisposable
{
    private readonly string _carpeta =
        Path.Combine(Path.GetTempPath(), "reactor-pruebas", Guid.NewGuid().ToString("N"));

    public RepositoriosJsonTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* no importa si no se puede limpiar */ }
    }

    private string Ruta(string nombre) => Path.Combine(_carpeta, nombre);

    private static Usuario NuevoUsuario(string correo = "ana@correo.com") =>
        new(Guid.NewGuid(), "Ana", correo, "hash-de-prueba");

    // ---------- Usuarios ----------

    [Fact]
    public async Task Usuario_GuardadoSePuedeBuscarPorIdYPorCorreo()
    {
        var repo = new JsonUsuarioRepositorio(Ruta("usuarios.json"));
        var usuario = NuevoUsuario();
        await repo.GuardarAsync(usuario);

        var porId = await repo.ObtenerPorIdAsync(usuario.Id);
        var porCorreo = await repo.ObtenerPorCorreoAsync("ANA@correo.com");

        Assert.NotNull(porId);
        Assert.Equal(usuario.Id, porCorreo?.Id);
    }

    [Fact]
    public async Task Usuario_ConservaVerificadoYAvatarAlRecargar()
    {
        var ruta = Ruta("usuarios.json");
        var usuario = NuevoUsuario();
        var avatar = Guid.NewGuid();
        usuario.MarcarVerificado();
        usuario.ElegirAvatar(avatar);
        await new JsonUsuarioRepositorio(ruta).GuardarAsync(usuario);

        var recargado = await new JsonUsuarioRepositorio(ruta).ObtenerPorIdAsync(usuario.Id);

        Assert.NotNull(recargado);
        Assert.True(recargado.Verificado);
        Assert.Equal(avatar, recargado.AvatarId);
        Assert.Equal("hash-de-prueba", recargado.HashContrasena);
    }

    [Fact]
    public async Task Usuario_GuardarDeNuevo_ActualizaSinDuplicar()
    {
        var ruta = Ruta("usuarios.json");
        var repo = new JsonUsuarioRepositorio(ruta);
        var usuario = NuevoUsuario();
        await repo.GuardarAsync(usuario);

        usuario.MarcarVerificado();
        await repo.GuardarAsync(usuario);

        var recargado = await new JsonUsuarioRepositorio(ruta).ObtenerPorIdAsync(usuario.Id);
        Assert.True(recargado!.Verificado);
        var almacen = new JsonFileStore<System.Text.Json.Nodes.JsonObject>(ruta);
        Assert.Single(await almacen.LeerAsync());
    }

    [Fact]
    public async Task Usuario_ConCorreoYaUsado_Falla()
    {
        var repo = new JsonUsuarioRepositorio(Ruta("usuarios.json"));
        await repo.GuardarAsync(NuevoUsuario("ana@correo.com"));

        await Assert.ThrowsAsync<DominioException>(
            () => repo.GuardarAsync(NuevoUsuario("ANA@correo.com")));
    }

    [Fact]
    public async Task Usuario_Inexistente_DevuelveNull()
    {
        var repo = new JsonUsuarioRepositorio(Ruta("usuarios.json"));

        Assert.Null(await repo.ObtenerPorIdAsync(Guid.NewGuid()));
        Assert.Null(await repo.ObtenerPorCorreoAsync("nadie@correo.com"));
    }

    // ---------- Avatares ----------

    [Fact]
    public async Task Avatares_SinArchivo_SeCarganDeLaSemilla()
    {
        var id = Guid.NewGuid();
        var semilla = Ruta("semilla-avatares.json");
        await File.WriteAllTextAsync(semilla,
            $$"""[{ "id": "{{id}}", "nombre": "Volt", "color": "#FFD400" }]""");
        var repo = new JsonAvatarRepositorio(Ruta("avatares.json"), semilla);

        var lista = await repo.ListarAsync();
        var uno = await repo.ObtenerPorIdAsync(id);

        Assert.Single(lista);
        Assert.Equal("Volt", uno?.Nombre);
        Assert.Equal("#FFD400", uno?.Color);
    }

    [Fact]
    public async Task Avatares_ConIdDesconocido_DevuelveNull()
    {
        var repo = new JsonAvatarRepositorio(Ruta("avatares.json"));

        Assert.Null(await repo.ObtenerPorIdAsync(Guid.NewGuid()));
    }
}
