using UnityEngine;

namespace Reactor.Cliente.Arena
{
    public static class Colores
    {
        public static Color Desde(string hex, Color porDefecto) =>
            !string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : porDefecto;

        /// <summary>Devuelve un "#RRGGBB" válido para las etiquetas de color de TextMeshPro.</summary>
        public static string HexSeguro(string hex) => "#" + ColorUtility.ToHtmlStringRGB(Desde(hex, Color.white));
    }
}
