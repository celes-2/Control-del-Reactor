using Dominio.Usuarios;

namespace Aplicacion.Puertos;

/// <summary>
/// Puerto de salida: lo que los casos de uso necesitan para guardar y buscar usuarios.
/// La capa de aplicación solo conoce esta interfaz; el archivo JSON queda en Infraestructura.
/// </summary>
public interface IUsuarioRepo
{
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca por nombre de usuario sin distinguir mayúsculas.</summary>
    Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default);

    Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default);

    /// <summary>Crea el usuario o actualiza el que tenga el mismo Id. Rechaza un correo o nombre ya usado por otro usuario.</summary>
    Task GuardarAsync(Usuario usuario, CancellationToken ct = default);
}
