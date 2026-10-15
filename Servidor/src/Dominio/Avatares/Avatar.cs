using Dominio.Comun;

namespace Dominio.Avatares;

/// <summary>
/// Personaje que un usuario puede elegir. Hoy es solo apariencia; los avatares
/// iniciales se cargan desde Servidor/Data/seed (módulo perfil-avatar).
/// </summary>
public sealed class Avatar
{
    public Guid Id { get; }
    public string Nombre { get; }

    /// <summary>Color en hexadecimal, por ejemplo "#FF8800". Unity lo usa para teñir el sprite.</summary>
    public string Color { get; }

    public Avatar(Guid id, string nombre, string color)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DominioException("El avatar necesita un nombre.");
        if (string.IsNullOrWhiteSpace(color))
            throw new DominioException("El avatar necesita un color.");

        Id = id;
        Nombre = nombre.Trim();
        Color = color.Trim();
    }
}
