using Dominio.Avatares;

namespace Aplicacion.Puertos;

/// <summary>Puerto de salida: guarda y entrega las imágenes de avatar que cargan los jugadores.</summary>
public interface IAvatarPropioRepo
{
    /// <summary>Guarda la imagen nueva del jugador y borra la anterior (cada jugador conserva una sola).</summary>
    Task GuardarReemplazandoAsync(AvatarPropio avatar, byte[] contenido, CancellationToken ct = default);

    Task<AvatarPropio?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<AvatarPropio?> ObtenerDeUsuarioAsync(Guid duenoId, CancellationToken ct = default);

    Task<byte[]?> LeerContenidoAsync(Guid id, CancellationToken ct = default);
}
