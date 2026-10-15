using Dominio.Comun;

namespace Dominio.Usuarios;

/// <summary>
/// Cuenta de un jugador (módulo registro). Nunca guarda la contraseña en claro:
/// recibe el hash ya calculado por la capa de infraestructura.
/// </summary>
public sealed class Usuario
{
    public Guid Id { get; }
    public string Nombre { get; private set; }
    public string Correo { get; }
    public string HashContrasena { get; private set; }
    public bool Verificado { get; private set; }
    public Guid? AvatarId { get; private set; }

    public Usuario(Guid id, string nombre, string correo, string hashContrasena)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DominioException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
            throw new DominioException("El correo no es válido.");
        if (string.IsNullOrWhiteSpace(hashContrasena))
            throw new DominioException("Falta la contraseña.");

        Id = id;
        Nombre = nombre.Trim();
        Correo = correo.Trim().ToLowerInvariant();
        HashContrasena = hashContrasena;
    }

    public void MarcarVerificado() => Verificado = true;

    public void CambiarContrasena(string nuevoHash)
    {
        if (string.IsNullOrWhiteSpace(nuevoHash))
            throw new DominioException("Falta la nueva contraseña.");

        HashContrasena = nuevoHash;
    }

    public void ElegirAvatar(Guid avatarId) => AvatarId = avatarId;

    public void CambiarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DominioException("El nombre es obligatorio.");

        Nombre = nombre.Trim();
    }
}
