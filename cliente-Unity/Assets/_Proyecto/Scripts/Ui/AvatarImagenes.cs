using System.Collections.Generic;
using UnityEngine;

namespace Reactor.Cliente.Ui
{
    /// <summary>
    /// Imágenes de los avatares PREDETERMINADOS. Los PNG viven en Assets/_Proyecto/Resources/Avatares/
    /// con el nombre en minúsculas: volt.png, pyro.png, aqua.png, terra.png, nova.png.
    /// Si falta la imagen (o el avatar no es predeterminado) devuelve null y cada vista usa su círculo de color.
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

        /// <summary>Sprite del avatar predeterminado con ese id, o null.</summary>
        public static Sprite DePredeterminado(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId)) return null;
            if (!NombrePorId.TryGetValue(avatarId.ToLowerInvariant(), out var nombre)) return null;
            return PorNombre(nombre);
        }

        /// <summary>Sprite por nombre (volt, pyro...), usado por el Perfil que conoce el nombre del catálogo.</summary>
        public static Sprite PorNombre(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            var clave = nombre.ToLowerInvariant();
            if (cache.TryGetValue(clave, out var s)) return s;
            s = Resources.Load<Sprite>("Avatares/" + clave);
            cache[clave] = s; // también guarda null para no reintentar
            return s;
        }
    }
}
