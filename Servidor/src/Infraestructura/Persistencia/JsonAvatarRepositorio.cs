using Aplicacion.Puertos;
using Dominio.Avatares;

namespace Infraestructura.Persistencia;

internal sealed record AvatarRegistro(Guid Id, string Nombre, string Color)
{
    public Avatar ADominio() => new(Id, Nombre, Color);
}

/// <summary>
/// Adaptador del puerto <see cref="IAvatarRepo"/>. Si "avatares.json" todavía no existe,
/// parte de los avatares iniciales del archivo semilla (Servidor/Data/seed/avatares.json).
/// </summary>
public sealed class JsonAvatarRepositorio : IAvatarRepo
{
    private readonly JsonFileStore<AvatarRegistro> _almacen;

    public JsonAvatarRepositorio(string rutaArchivo, string? rutaSemilla = null) =>
        _almacen = new JsonFileStore<AvatarRegistro>(rutaArchivo, rutaSemilla);

    public async Task<IReadOnlyList<Avatar>> ListarAsync(CancellationToken ct = default)
    {
        var todos = await _almacen.LeerAsync(ct);
        return todos.Select(a => a.ADominio()).ToList();
    }

    public async Task<Avatar?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var todos = await _almacen.LeerAsync(ct);
        return todos.FirstOrDefault(a => a.Id == id)?.ADominio();
    }
}
