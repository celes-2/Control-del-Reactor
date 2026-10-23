using Dominio.Comun;

namespace Dominio.Avatares;

/// <summary>
/// Imagen que un jugador cargó desde su disco para usarla como avatar (CA-06.3).
/// El dominio solo conoce a quién pertenece y de qué tipo es; los bytes viven en el almacén de archivos.
/// </summary>
public sealed class AvatarPropio
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";

    public Guid Id { get; }
    public Guid DuenoId { get; }
    public string TipoContenido { get; }

    public AvatarPropio(Guid id, Guid duenoId, string tipoContenido)
    {
        if (id == Guid.Empty || duenoId == Guid.Empty)
            throw new DominioException("El avatar propio necesita un id y un dueño.");
        if (tipoContenido != Png && tipoContenido != Jpeg)
            throw new DominioException("Solo se aceptan imágenes PNG o JPG.");

        Id = id;
        DuenoId = duenoId;
        TipoContenido = tipoContenido;
    }

    public string Extension => TipoContenido == Png ? ".png" : ".jpg";
}
