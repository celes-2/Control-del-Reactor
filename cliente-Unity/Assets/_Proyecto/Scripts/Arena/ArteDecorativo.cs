using System.Collections.Generic;
using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Adornos temáticos de la arena, dibujados por código en pixel art de 8 x 8 (no hace falta ningún archivo).
    /// Cada tema tiene cuatro: a, b, c y d. El archivo del escenario dice dónde va cada uno.
    /// Para usar imágenes propias basta con reemplazar Para(...) por un Sprite importado.
    /// </summary>
    public static class ArteDecorativo
    {
        // K negro azulado, S acero, B azul oscuro, C celeste, W blanco, G verde, Y amarillo, R rojo,
        // O oliva oscuro, L oliva claro, T arena, D arena oscura, P rosa.
        static readonly Dictionary<char, Color> Paleta = new Dictionary<char, Color>
        {
            ['K'] = Hex("#2b2f3a"), ['S'] = Hex("#8a93a3"), ['B'] = Hex("#1f3d7a"), ['C'] = Hex("#4fd8ff"),
            ['W'] = Hex("#e8f6ff"), ['G'] = Hex("#43d17a"), ['Y'] = Hex("#ffd23f"), ['R'] = Hex("#e5484d"),
            ['O'] = Hex("#5a6b36"), ['L'] = Hex("#8a9a52"), ['T'] = Hex("#d8c08a"), ['D'] = Hex("#b59d62"),
            ['P'] = Hex("#ff7ac8"),
        };

        static readonly Dictionary<string, string[][]> Temas = new Dictionary<string, string[][]>
        {
            // Centro de investigación: monitor, matraz, caja de muestras, microscopio.
            ["investigacion"] = new[]
            {
                new[] { ".KKKKKK.", ".KBBBBK.", ".KBCCBK.", ".KBBBBK.", ".KKKKKK.", "...KK...", "..KKKK..", "........" },
                new[] { "...WW...", "...WW...", "..WWWW..", ".WGGGGW.", ".WGGGGW.", ".WGGGGW.", "..WWWW..", "........" },
                new[] { "KKKKKKKK", "KSSSSSSK", "KSSRRSSK", "KSRRRRSK", "KSRRRRSK", "KSSRRSSK", "KSSSSSSK", "KKKKKKKK" },
                new[] { "...KK...", "..KKS...", "...KK...", "..KSK...", "..KSK.K.", ".KKKKKK.", "KKKKKKKK", "........" },
            },
            // Laboratorio subterráneo: barril químico, válvula, tanque, luz de emergencia (parpadea).
            ["laboratorio"] = new[]
            {
                new[] { ".YYYYYY.", ".KYYYYK.", ".YYKKYY.", ".YYKKYY.", ".KYYYYK.", ".YYYYYY.", ".KKKKKK.", "........" },
                new[] { "..RRRR..", ".R....R.", ".R.SS.R.", "..RRRR..", "...SS...", "SSSSSSSS", "SSSSSSSS", "........" },
                new[] { "..SSSS..", ".SGGGGS.", ".SGGGGS.", ".SGGGGS.", ".SGGGGS.", ".SGGGGS.", "..SSSS..", "........" },
                new[] { "........", "...RR...", "..RRRR..", "..RRRR..", ".KKKKKK.", ".KKKKKK.", "........", "........" },
            },
            // Estación espacial: panel de control, antena, cápsula de oxígeno, baliza (parpadea).
            ["estacion"] = new[]
            {
                new[] { "KKKKKKKK", "KBBBBBBK", "KBCBYBGK", "KBBBBBBK", "KBYBCBRK", "KBBBBBBK", "KKKKKKKK", "........" },
                new[] { ".....CC.", "....CC..", ".SSSC...", "SSSSS...", ".SSS....", "..KK....", "..KK....", ".KKKK..." },
                new[] { "..CCCC..", ".CWWCCC.", ".CWWCCC.", ".CCCCCC.", ".CCCCCC.", ".CCCCCC.", "..CCCC..", "........" },
                new[] { "........", "........", "...CC...", "..CWWC..", "..CWWC..", "...CC...", "........", "........" },
            },
            // Complejo militar: caja de munición, sacos de arena, torre de radar, barril de combustible.
            ["militar"] = new[]
            {
                new[] { "OOOOOOOO", "OLLLLLLO", "OLOOOOLO", "OLOLLOLO", "OLOLLOLO", "OLOOOOLO", "OLLLLLLO", "OOOOOOOO" },
                new[] { "........", "........", ".TTTTTT.", "TTDTTDTT", "TTTTTTTT", "DTTDTTDT", "TTTTTTTT", "........" },
                new[] { "...K....", "..KKK...", "...K....", "..KKK...", "...K....", ".KKKKK..", ".KKKKK..", "........" },
                new[] { ".RRRRRR.", ".RKRRKR.", ".RRRRRR.", ".RKRRKR.", ".RRRRRR.", ".RKRRKR.", ".RRRRRR.", "........" },
            },
        };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>El adorno 'a'-'d' del tema; null si no existe.</summary>
        public static Sprite Para(string tema, char tipo)
        {
            var indice = char.ToLowerInvariant(tipo) - 'a';
            if (tema == null || !Temas.TryGetValue(tema, out var arte) || indice < 0 || indice >= arte.Length) return null;

            var clave = tema + tipo;
            if (Cache.TryGetValue(clave, out var guardado) && guardado != null) return guardado;

            var filas = arte[indice];
            var textura = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var letra = filas[y][x];
                    textura.SetPixel(x, 7 - y, Paleta.TryGetValue(letra, out var c) ? c : Color.clear);
                }
            textura.Apply();
            var sprite = Sprite.Create(textura, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            Cache[clave] = sprite;
            return sprite;
        }

        /// <summary>Los adornos 'd' de estos temas son luces y parpadean.</summary>
        public static bool Parpadea(string tema, char tipo) =>
            char.ToLowerInvariant(tipo) == 'd' && (tema == "laboratorio" || tema == "estacion");

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
