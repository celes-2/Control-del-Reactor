using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infraestructura.Persistencia;

/// <summary>
/// Guarda una lista de objetos en un archivo JSON (la "base de datos" del proyecto).
///
///  - Escritura atómica: se escribe un archivo temporal y luego se reemplaza el original,
///    así un corte de luz a mitad de la escritura no deja el archivo a medias.
///  - Respaldo: el reemplazo deja la versión anterior en "archivo.json.bak".
///    Si el archivo principal está dañado, se recupera del respaldo.
///  - Semilla: si el archivo todavía no existe, se parte del contenido de un archivo "semilla"
///    (por ejemplo los avatares iniciales). Se crea el archivo real en la primera escritura.
///  - Concurrencia: un candado asegura que lecturas y modificaciones no se mezclen.
///    Debe haber UNA sola instancia por archivo (se registra como singleton).
/// </summary>
public sealed class JsonFileStore<T> where T : class
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _ruta;
    private readonly string? _rutaSemilla;
    private readonly SemaphoreSlim _candado = new(1, 1);

    public JsonFileStore(string rutaArchivo, string? rutaSemilla = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaArchivo);
        _ruta = rutaArchivo;
        _rutaSemilla = rutaSemilla;
    }

    public async Task<IReadOnlyList<T>> LeerAsync(CancellationToken ct = default)
    {
        await _candado.WaitAsync(ct);
        try
        {
            return await CargarAsync(ct);
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>
    /// Lee la lista, aplica el cambio y la guarda, todo dentro del candado.
    /// Si el cambio lanza una excepción, el archivo no se toca.
    /// </summary>
    public async Task ModificarAsync(Action<List<T>> cambio, CancellationToken ct = default)
    {
        await ModificarYObtenerAsync(lista =>
        {
            cambio(lista);
            return true;
        }, ct);
    }

    /// <summary>Igual que <see cref="ModificarAsync"/>, pero el cambio también devuelve un resultado.</summary>
    public async Task<TResultado> ModificarYObtenerAsync<TResultado>(
        Func<List<T>, TResultado> cambio, CancellationToken ct = default)
    {
        await _candado.WaitAsync(ct);
        try
        {
            var datos = await CargarAsync(ct);
            var resultado = cambio(datos);
            await GuardarAsync(datos, ct);
            return resultado;
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<List<T>> CargarAsync(CancellationToken ct)
    {
        if (File.Exists(_ruta))
        {
            try
            {
                return await LeerArchivoAsync(_ruta, ct);
            }
            catch (JsonException)
            {
                var respaldo = _ruta + ".bak";
                if (File.Exists(respaldo))
                {
                    try
                    {
                        return await LeerArchivoAsync(respaldo, ct);
                    }
                    catch (JsonException)
                    {
                        // El respaldo también está dañado: se informa abajo.
                    }
                }

                throw new InvalidDataException(
                    $"El archivo de datos '{_ruta}' está dañado y no hay un respaldo válido.");
            }
        }

        if (_rutaSemilla is not null && File.Exists(_rutaSemilla))
            return await LeerArchivoAsync(_rutaSemilla, ct);

        return [];
    }

    private static async Task<List<T>> LeerArchivoAsync(string ruta, CancellationToken ct)
    {
        await using var flujo = File.OpenRead(ruta);
        return await JsonSerializer.DeserializeAsync<List<T>>(flujo, Opciones, ct) ?? [];
    }

    private async Task GuardarAsync(List<T> datos, CancellationToken ct)
    {
        var carpeta = Path.GetDirectoryName(Path.GetFullPath(_ruta))!;
        Directory.CreateDirectory(carpeta);

        var temporal = _ruta + ".tmp";
        await using (var flujo = new FileStream(
            temporal, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(flujo, datos, Opciones, ct);
            await flujo.FlushAsync(ct);
            flujo.Flush(flushToDisk: true);
        }

        if (File.Exists(_ruta))
            File.Replace(temporal, _ruta, _ruta + ".bak");
        else
            File.Move(temporal, _ruta);
    }
}
