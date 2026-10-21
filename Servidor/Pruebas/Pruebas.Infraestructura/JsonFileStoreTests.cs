using Infraestructura.Persistencia;
using Xunit;

namespace PruebasInfraestructura;

public sealed class JsonFileStoreTests : IDisposable
{
    public sealed record Elemento(int Id, string Texto);

    private readonly string _carpeta =
        Path.Combine(Path.GetTempPath(), "reactor-pruebas", Guid.NewGuid().ToString("N"));

    public JsonFileStoreTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* no importa si no se puede limpiar */ }
    }

    private string Ruta(string nombre) => Path.Combine(_carpeta, nombre);

    [Fact]
    public async Task Leer_SinArchivoNiSemilla_DevuelveListaVacia()
    {
        var almacen = new JsonFileStore<Elemento>(Ruta("datos.json"));

        var datos = await almacen.LeerAsync();

        Assert.Empty(datos);
    }

    [Fact]
    public async Task Modificar_GuardaYOtraInstanciaPuedeLeer()
    {
        var ruta = Ruta("datos.json");
        await new JsonFileStore<Elemento>(ruta)
            .ModificarAsync(lista => lista.Add(new Elemento(1, "uno")));

        var datos = await new JsonFileStore<Elemento>(ruta).LeerAsync();

        Assert.Equal(new Elemento(1, "uno"), Assert.Single(datos));
    }

    [Fact]
    public async Task Leer_SinArchivo_UsaLaSemilla()
    {
        var semilla = Ruta("semilla.json");
        await File.WriteAllTextAsync(semilla, """[{ "id": 7, "texto": "inicial" }]""");
        var almacen = new JsonFileStore<Elemento>(Ruta("datos.json"), semilla);

        var datos = await almacen.LeerAsync();

        Assert.Equal(new Elemento(7, "inicial"), Assert.Single(datos));
        Assert.False(File.Exists(Ruta("datos.json"))); // leer no crea el archivo
    }

    [Fact]
    public async Task Modificar_ParteDeLaSemillaYLuegoPersisteTodo()
    {
        var semilla = Ruta("semilla.json");
        await File.WriteAllTextAsync(semilla, """[{ "id": 7, "texto": "inicial" }]""");
        var ruta = Ruta("datos.json");

        await new JsonFileStore<Elemento>(ruta, semilla)
            .ModificarAsync(lista => lista.Add(new Elemento(8, "nuevo")));

        var datos = await new JsonFileStore<Elemento>(ruta, semilla).LeerAsync();
        Assert.Equal(2, datos.Count);
    }

    [Fact]
    public async Task Modificar_DosVeces_DejaUnRespaldo()
    {
        var ruta = Ruta("datos.json");
        var almacen = new JsonFileStore<Elemento>(ruta);

        await almacen.ModificarAsync(lista => lista.Add(new Elemento(1, "uno")));
        await almacen.ModificarAsync(lista => lista.Add(new Elemento(2, "dos")));

        Assert.True(File.Exists(ruta + ".bak"));
        Assert.False(File.Exists(ruta + ".tmp"));
    }

    [Fact]
    public async Task Leer_ConArchivoDanado_RecuperaDelRespaldo()
    {
        var ruta = Ruta("datos.json");
        var almacen = new JsonFileStore<Elemento>(ruta);
        await almacen.ModificarAsync(lista => lista.Add(new Elemento(1, "uno")));
        await almacen.ModificarAsync(lista => lista.Add(new Elemento(2, "dos")));
        await File.WriteAllTextAsync(ruta, "{{{ esto no es json");

        var datos = await new JsonFileStore<Elemento>(ruta).LeerAsync();

        // El respaldo guarda el estado anterior a la última escritura.
        Assert.Equal(new Elemento(1, "uno"), Assert.Single(datos));
    }

    [Fact]
    public async Task Leer_ConArchivoDanadoYSinRespaldo_Falla()
    {
        var ruta = Ruta("datos.json");
        await File.WriteAllTextAsync(ruta, "{{{ esto no es json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new JsonFileStore<Elemento>(ruta).LeerAsync());
    }

    [Fact]
    public async Task Modificar_QueLanzaExcepcion_NoCambiaElArchivo()
    {
        var ruta = Ruta("datos.json");
        var almacen = new JsonFileStore<Elemento>(ruta);
        await almacen.ModificarAsync(lista => lista.Add(new Elemento(1, "uno")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            almacen.ModificarAsync(lista =>
            {
                lista.Add(new Elemento(2, "dos"));
                throw new InvalidOperationException("falla a propósito");
            }));

        var datos = await almacen.LeerAsync();
        Assert.Equal(new Elemento(1, "uno"), Assert.Single(datos));
    }

    [Fact]
    public async Task Modificar_EnParalelo_NoPierdeDatos()
    {
        var almacen = new JsonFileStore<Elemento>(Ruta("datos.json"));

        var tareas = Enumerable.Range(1, 50).Select(i =>
            almacen.ModificarAsync(lista => lista.Add(new Elemento(i, $"n{i}"))));
        await Task.WhenAll(tareas);

        var datos = await almacen.LeerAsync();
        Assert.Equal(50, datos.Count);
        Assert.Equal(50, datos.Select(d => d.Id).Distinct().Count());
    }
}
