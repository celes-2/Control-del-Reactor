using TMPro;
using UnityEngine;

namespace Reactor.Cliente.Ui
{
    /// <summary>
    /// Tipografía Press Start 2P (licencia OFL) para todo el juego.
    /// El archivo está en Assets/Resources/Fuentes/PressStart2P-Regular.ttf; el atlas se arma en ejecución,
    /// así que no hace falta generar ningún asset de TextMesh Pro a mano.
    /// </summary>
    public static class FuentePixel
    {
        /// <summary>Apagada: se volvió a la tipografía original. Poner true para usar Press Start 2P otra vez.</summary>
        const bool Activa = false;

        static TMP_FontAsset fuente;

        public static TMP_FontAsset Obtener()
        {
            if (!Activa) return null;
            if (fuente != null) return fuente;
            var archivo = Resources.Load<Font>("Fuentes/PressStart2P-Regular");
            if (archivo == null)
            {
                Debug.LogWarning("No se encontró Resources/Fuentes/PressStart2P-Regular.ttf; se usa la fuente por defecto.");
                return null;
            }
            fuente = TMP_FontAsset.CreateFontAsset(archivo, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fuente.name = "PressStart2P SDF (dinámica)";
            return fuente;
        }
    }
}
