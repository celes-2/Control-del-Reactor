using Infraestructura.Persistencia;

namespace PruebasInfraestructura;

public class UsuariosSemillaTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "reactor-semilla-" + Guid.NewGuid());

    public UsuariosSemillaTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    private static string Semilla()
    {
        // La prueba usa el mismo archivo que se versiona en Servidor/Data/seed.
        var carpeta = AppContext.BaseDirectory;
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta, "Data", "seed", "usuarios.json")))
            carpeta = Path.GetDirectoryName(carpeta);
        return Path.Combine(carpeta ?? throw new FileNotFoundException("No se encontró Data/seed/usuarios.json"),
            "Data", "seed", "usuarios.json");
    }

    [Fact]
    public async Task SinArchivoDeDatos_ParteDeLosUsuariosSemilla_VerificadosYConAvatar()
    {
        var repo = new JsonUsuarioRepositorio(Path.Combine(_carpeta, "usuarios.json"), Semilla());

        var ana = await repo.ObtenerPorCorreoAsync("ana@prueba.local");

        Assert.NotNull(ana);
        Assert.True(ana.Verificado);
        Assert.NotNull(ana.AvatarId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), ana.Id);
    }

    [Fact]
    public async Task ConUnaSemillaInexistente_ElRepositorioQuedaVacio()
    {
        var repo = new JsonUsuarioRepositorio(
            Path.Combine(_carpeta, "usuarios.json"), Path.Combine(_carpeta, "no-existe.json"));

        Assert.Null(await repo.ObtenerPorCorreoAsync("ana@prueba.local"));
    }
}
