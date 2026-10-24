using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Ui
{
    /// <summary>
    /// Imágenes de los avatares. Los PREDETERMINADOS viven en Assets/_Proyecto/Resources/Avatares/ (pablo.png, ...);
    /// los PROPIOS (subidos por el jugador) se descargan del servidor con <see cref="Cache"/>.
    /// Si falta la imagen cada vista deja su círculo de color.
    /// </summary>
    public static class AvatarImagenes
    {
        // Mismos ids que Servidor/Data/seed/avatares.json
        static readonly Dictionary<string, string> NombrePorId = new Dictionary<string, string>
        {
            { "cc2ec6b9-97f9-41ca-8985-2fc3280f7e62", "pablo" },
            { "a26fe6bc-94c8-44ee-a6c7-3ef34f66442b", "tasha" },
            { "96337b54-24be-4f32-bfc9-0bd8a13a92f5", "austin" },
            { "c4ab2c3c-700d-4918-be03-7df0682380f0", "tyrone" },
            { "5e1d7b2a-3c64-4f0e-9a58-8d2f1c6b7a43", "uniqua" },
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Descarga de avatares propios. Lo asigna CompositionRoot al arrancar.</summary>
        public static IAvatarCache Cache { get; set; }

        /// <summary>Sprite del avatar predeterminado con ese id, o null.</summary>
        public static Sprite DePredeterminado(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId)) return null;
            if (!NombrePorId.TryGetValue(avatarId.ToLowerInvariant(), out var nombre)) return null;
            return PorNombre(nombre);
        }

        /// <summary>Sprite por nombre (pablo, tasha...), usado por el Perfil que conoce el nombre del catálogo.</summary>
        public static Sprite PorNombre(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            var clave = nombre.ToLowerInvariant();
            if (cache.TryGetValue(clave, out var s)) return s;
            s = Resources.Load<Sprite>("Avatares/" + clave);
            cache[clave] = s; // también guarda null para no reintentar
            return s;
        }

        /// <summary>
        /// Pone en <paramref name="destino"/> la imagen del avatar: la predeterminada al instante, o la propia
        /// cuando termina de descargarse. Si no hay imagen, no toca nada (queda el círculo de color).
        /// Es segura si la vista se destruye antes de que llegue la descarga.
        /// </summary>
        public static void Aplicar(Image destino, string avatarId)
        {
            if (destino == null || string.IsNullOrEmpty(avatarId)) return;

            var predeterminado = DePredeterminado(avatarId);
            if (predeterminado != null)
            {
                Poner(destino, predeterminado);
                return;
            }

            if (Cache == null || !Guid.TryParse(avatarId, out _)) return;
            _ = DescargarYPoner(destino, avatarId);
        }

        /// <summary>
        /// Para vistas que no usan <see cref="Image"/> (como la Arena): si el avatar es PROPIO lo descarga y llama a
        /// <paramref name="alRecibir"/> con el sprite cuando llega. Con un avatar predeterminado o sin imagen no hace nada.
        /// </summary>
        public static void CargarPropio(string avatarId, Action<Sprite> alRecibir)
        {
            if (alRecibir == null || string.IsNullOrEmpty(avatarId)) return;
            if (Cache == null)
            {
                Debug.LogWarning("AvatarImagenes.Cache es null: falta asignarla en CompositionRoot.");
                return;
            }
            if (DePredeterminado(avatarId) != null || !Guid.TryParse(avatarId, out _)) return;
            Debug.Log($"Avatar propio {avatarId}: descargando imagen");
            _ = DescargarYLlamar(avatarId, alRecibir);
        }

        static async Task DescargarYLlamar(string avatarId, Action<Sprite> alRecibir)
        {
            try
            {
                var sprite = await Cache.ObtenerAsync(avatarId);
                if (sprite != null) alRecibir(sprite);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Avatar {avatarId}: no se pudo mostrar la imagen propia ({ex.Message})");
            }
        }

        static async Task DescargarYPoner(Image destino, string avatarId)
        {
            try
            {
                var sprite = await Cache.ObtenerAsync(avatarId);
                if (sprite != null && destino != null) Poner(destino, sprite);
            }
            catch (Exception)
            {
                // Sin imagen no se rompe nada: se queda el círculo de color.
            }
        }

        static void Poner(Image destino, Sprite sprite)
        {
            destino.sprite = sprite;
            destino.color = Color.white;
            destino.preserveAspect = true;
        }
    }
}
