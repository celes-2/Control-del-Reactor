using Aplicacion.Puertos;
using Dominio.Avatares;

namespace Aplicacion.CasosDeUso;

public sealed record ImagenAvatar(byte[] Contenido, string TipoContenido);

/// <summary>
/// CA-06.3 / CA-06.4: el jugador carga una imagen de su disco y queda como su avatar.
/// Se valida el contenido real del archivo (firma PNG o JPG), no solo la extensión, y se limita el tamaño.
/// </summary>
public sealed class AvatarPropioService(IUsuarioRepo usuarios, IAvatarPropioRepo propios)
{
    public const int MaxBytes = 1_000_000;

    public async Task<Guid> SubirAsync(Guid userId, byte[]? contenido, CancellationToken ct = default)
    {
        if (contenido is null || contenido.Length == 0)
            throw new AuthException("IMAGEN_VACIA", "No se recibió ninguna imagen.");
        if (contenido.Length > MaxBytes)
            throw new AuthException("IMAGEN_MUY_GRANDE", "La imagen no puede pesar más de 1 MB.");

        var tipo = DetectarTipo(contenido)
                   ?? throw new AuthException("IMAGEN_NO_SOPORTADA", "Solo se aceptan imágenes PNG o JPG.");

        var usuario = await usuarios.ObtenerPorIdAsync(userId, ct)
                      ?? throw new AuthException("USUARIO_NO_ENCONTRADO", "No se encontró el usuario.");

        var avatar = new AvatarPropio(Guid.NewGuid(), userId, tipo);
        await propios.GuardarReemplazandoAsync(avatar, contenido, ct);

        usuario.ElegirAvatar(avatar.Id);
        await usuarios.GuardarAsync(usuario, ct);
        return avatar.Id;
    }

    /// <summary>La imagen de un avatar propio, o null si ese id no es una imagen cargada (los avatares predeterminados solo tienen color).</summary>
    public async Task<ImagenAvatar?> ObtenerImagenAsync(Guid avatarId, CancellationToken ct = default)
    {
        var avatar = await propios.ObtenerAsync(avatarId, ct);
        if (avatar is null) return null;

        var contenido = await propios.LeerContenidoAsync(avatarId, ct);
        return contenido is null ? null : new ImagenAvatar(contenido, avatar.TipoContenido);
    }

    /// <summary>Reconoce PNG y JPG por su firma; cualquier otra cosa devuelve null.</summary>
    public static string? DetectarTipo(byte[] datos)
    {
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (datos.Length >= png.Length && datos.AsSpan(0, png.Length).SequenceEqual(png))
            return AvatarPropio.Png;

        if (datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF)
            return AvatarPropio.Jpeg;

        return null;
    }
}
