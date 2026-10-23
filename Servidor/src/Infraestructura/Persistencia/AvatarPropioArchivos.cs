using Aplicacion.Puertos;
using Dominio.Avatares;

namespace Infraestructura.Persistencia;

/// <summary>Forma de un avatar propio en avatares-propios.json (los bytes van aparte, en la carpeta "avatares").</summary>
public sealed class AvatarPropioRegistro
{
    public Guid Id { get; set; }
    public Guid DuenoId { get; set; }
    public string TipoContenido { get; set; } = "";

    public static AvatarPropioRegistro Desde(AvatarPropio a) =>
        new() { Id = a.Id, DuenoId = a.DuenoId, TipoContenido = a.TipoContenido };

    public AvatarPropio ADominio() => new(Id, DuenoId, TipoContenido);
}

/// <summary>
/// Adaptador del puerto <see cref="IAvatarPropioRepo"/>: el índice en "avatares-propios.json" y cada imagen como archivo
/// "{id}.png|jpg" dentro de "avatares/", todo bajo la carpeta de datos (volumen de Docker). El nombre del archivo sale
/// siempre de un Guid, nunca de texto del cliente.
/// </summary>
public sealed class ArchivoAvatarPropioRepositorio : IAvatarPropioRepo
{
    private readonly JsonFileStore<AvatarPropioRegistro> _indice;
    private readonly string _carpetaImagenes;

    public ArchivoAvatarPropioRepositorio(string carpetaDatos)
    {
        _indice = new JsonFileStore<AvatarPropioRegistro>(Path.Combine(carpetaDatos, "avatares-propios.json"));
        _carpetaImagenes = Path.Combine(carpetaDatos, "avatares");
    }

    public async Task GuardarReemplazandoAsync(AvatarPropio avatar, byte[] contenido, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        ArgumentNullException.ThrowIfNull(contenido);

        Directory.CreateDirectory(_carpetaImagenes);
        await File.WriteAllBytesAsync(Ruta(avatar), contenido, ct);

        var anteriores = await _indice.ModificarYObtenerAsync(lista =>
        {
            var viejos = lista.Where(x => x.DuenoId == avatar.DuenoId).ToList();
            lista.RemoveAll(x => x.DuenoId == avatar.DuenoId);
            lista.Add(AvatarPropioRegistro.Desde(avatar));
            return viejos;
        }, ct);

        foreach (var viejo in anteriores)
        {
            try { File.Delete(Ruta(viejo.ADominio())); }
            catch (IOException) { /* si no se puede borrar ahora, queda como archivo huérfano sin referencias */ }
        }
    }

    public async Task<AvatarPropio?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        (await _indice.LeerAsync(ct)).FirstOrDefault(x => x.Id == id)?.ADominio();

    public async Task<AvatarPropio?> ObtenerDeUsuarioAsync(Guid duenoId, CancellationToken ct = default) =>
        (await _indice.LeerAsync(ct)).FirstOrDefault(x => x.DuenoId == duenoId)?.ADominio();

    public async Task<byte[]?> LeerContenidoAsync(Guid id, CancellationToken ct = default)
    {
        var avatar = await ObtenerAsync(id, ct);
        if (avatar is null) return null;

        var ruta = Ruta(avatar);
        return File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta, ct) : null;
    }

    private string Ruta(AvatarPropio avatar) => Path.Combine(_carpetaImagenes, avatar.Id.ToString("N") + avatar.Extension);
}
