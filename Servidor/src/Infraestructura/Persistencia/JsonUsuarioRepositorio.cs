using Aplicacion.Puertos;
using Dominio.Comun;
using Dominio.Usuarios;

namespace Infraestructura.Persistencia;

/// <summary>
/// Forma en que un usuario se guarda en el JSON. Se usa un registro aparte para que
/// la entidad del dominio no tenga que cambiar su diseño para satisfacer al serializador.
/// </summary>
internal sealed record UsuarioRegistro(
    Guid Id, string Nombre, string Correo, string HashContrasena, bool Verificado, Guid? AvatarId)
{
    public static UsuarioRegistro Desde(Usuario u) =>
        new(u.Id, u.Nombre, u.Correo, u.HashContrasena, u.Verificado, u.AvatarId);

    public Usuario ADominio()
    {
        var usuario = new Usuario(Id, Nombre, Correo, HashContrasena);
        if (Verificado)
            usuario.MarcarVerificado();
        if (AvatarId is { } avatarId)
            usuario.ElegirAvatar(avatarId);
        return usuario;
    }
}

/// <summary>Adaptador del puerto <see cref="IUsuarioRepo"/> que guarda en "usuarios.json".</summary>
public sealed class JsonUsuarioRepositorio : IUsuarioRepo
{
    private readonly JsonFileStore<UsuarioRegistro> _almacen;

    public JsonUsuarioRepositorio(string rutaArchivo) =>
        _almacen = new JsonFileStore<UsuarioRegistro>(rutaArchivo);

    public async Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var todos = await _almacen.LeerAsync(ct);
        return todos.FirstOrDefault(u => u.Id == id)?.ADominio();
    }

    public async Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default)
    {
        var todos = await _almacen.LeerAsync(ct);
        return todos.FirstOrDefault(u => MismoCorreo(u.Correo, correo))?.ADominio();
    }

    public Task GuardarAsync(Usuario usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return _almacen.ModificarAsync(lista =>
        {
            if (lista.Any(u => u.Id != usuario.Id && MismoCorreo(u.Correo, usuario.Correo)))
                throw new DominioException("Ya existe una cuenta con ese correo.");

            var registro = UsuarioRegistro.Desde(usuario);
            var indice = lista.FindIndex(u => u.Id == usuario.Id);
            if (indice >= 0)
                lista[indice] = registro;
            else
                lista.Add(registro);
        }, ct);
    }

    private static bool MismoCorreo(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
