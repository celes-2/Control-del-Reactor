using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Sprites provisionales generados por código, para no depender de arte todavía.
    /// Cuando tengan tiles o avatares reales, se asignan en el Inspector y esto deja de usarse.
    /// </summary>
    public static class GeneradorSprites
    {
        static Sprite circulo;

        public static Sprite Cuadro(Color relleno, Color borde, int tam = 32, int grosor = 2)
        {
            var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixeles = new Color[tam * tam];
            for (int y = 0; y < tam; y++)
                for (int x = 0; x < tam; x++)
                {
                    bool esBorde = x < grosor || y < grosor || x >= tam - grosor || y >= tam - grosor;
                    pixeles[y * tam + x] = esBorde ? borde : relleno;
                }
            tex.SetPixels(pixeles);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
        }

        /// <summary>Círculo blanco de 1 unidad; el color se da con SpriteRenderer.color.</summary>
        public static Sprite Circulo()
        {
            if (circulo != null) return circulo;

            const int tam = 128;
            var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixeles = new Color[tam * tam];
            float radio = tam / 2f;
            for (int y = 0; y < tam; y++)
                for (int x = 0; x < tam; x++)
                {
                    float dx = x + 0.5f - radio;
                    float dy = y + 0.5f - radio;
                    float alfa = Mathf.Clamp01(radio - Mathf.Sqrt(dx * dx + dy * dy));
                    pixeles[y * tam + x] = new Color(1f, 1f, 1f, alfa);
                }
            tex.SetPixels(pixeles);
            tex.Apply();
            circulo = Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), tam);
            return circulo;
        }
    }
}
