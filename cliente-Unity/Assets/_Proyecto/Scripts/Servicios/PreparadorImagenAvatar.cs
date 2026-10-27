using System.IO;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Lee una imagen del disco y la deja lista para subir (CA-06.3).</summary>
    public interface IPreparadorImagen
    {
        /// <summary>PNG cuadrado y pequeño con la imagen de esa ruta. Lanza <see cref="ApiException"/> con un mensaje para el jugador si no sirve.</summary>
        byte[] Preparar(string ruta);
    }

    public sealed class PreparadorImagenAvatar : IPreparadorImagen
    {
        public const int Lado = 128;
        const long MaxBytesOriginal = 10L * 1024 * 1024;

        public byte[] Preparar(string ruta)
        {
            ruta = (ruta ?? "").Trim().Trim('"');   // al copiar la ruta en Windows suele traer comillas
            if (string.IsNullOrEmpty(ruta)) throw new ApiException("Escribe la ruta de la imagen.");
            if (!File.Exists(ruta)) throw new ApiException("No se encontró ese archivo. Revisa la ruta.");

            var extension = Path.GetExtension(ruta).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new ApiException("Solo se aceptan imágenes PNG o JPG.");
            if (new FileInfo(ruta).Length > MaxBytesOriginal)
                throw new ApiException("La imagen es demasiado grande (máximo 10 MB).");

            byte[] original;
            try
            {
                original = File.ReadAllBytes(ruta);
            }
            catch (IOException e)
            {
                throw new ApiException("No se pudo leer el archivo: " + e.Message);
            }

            var origen = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            RenderTexture destino = null;
            Texture2D salida = null;
            var anterior = RenderTexture.active;
            try
            {
                if (!origen.LoadImage(original)) throw new ApiException("El archivo no es una imagen válida.");

                // Recorte cuadrado desde el centro, escalado a Lado x Lado.
                var lado = Mathf.Min(origen.width, origen.height);
                var escala = new Vector2((float)lado / origen.width, (float)lado / origen.height);
                var desplazamiento = new Vector2((1f - escala.x) / 2f, (1f - escala.y) / 2f);

                destino = RenderTexture.GetTemporary(Lado, Lado, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(origen, destino, escala, desplazamiento);

                RenderTexture.active = destino;
                salida = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
                salida.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
                salida.Apply();
                return salida.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = anterior;
                if (destino != null) RenderTexture.ReleaseTemporary(destino);
                Object.Destroy(origen);
                if (salida != null) Object.Destroy(salida);
            }
        }
    }

    /// <summary>Servidor falso: no lee archivos, entrega un PNG mínimo para poder probar la pantalla.</summary>
    public sealed class PreparadorImagenFalso : IPreparadorImagen
    {
        public byte[] Preparar(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ApiException("Escribe la ruta de la imagen.");
            return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        }
    }
}
