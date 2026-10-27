using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reactor.Cliente.Ui
{
    /// <summary>
    /// Ayudas para armar pantallas sencillas por código (Canvas, textos, imágenes, botones),
    /// así cada escena solo necesita un objeto vacío con su Vista. Todo se coloca respecto al
    /// centro de un lienzo de 1920x1080 que se escala con la pantalla.
    /// </summary>
    public static class ConstructorUI
    {
        public static readonly Color Fondo = new Color(0.04f, 0.06f, 0.12f, 1f);
        public static readonly Color Dorado = new Color(1f, 0.82f, 0.2f, 1f);
        public static readonly Color Panel = new Color(0.11f, 0.14f, 0.22f, 1f);
        public static readonly Color Azul = new Color(0.2f, 0.55f, 0.95f, 1f);
        public static readonly Color Verde = new Color(0.18f, 0.72f, 0.42f, 1f);
        public static readonly Color Rojo = new Color(0.91f, 0.30f, 0.24f, 1f);
        public static readonly Color Gris = new Color(0.62f, 0.66f, 0.74f, 1f);
        public static readonly Color Arena = new Color(1.00f, 0.92f, 0.80f, 1f);

        static Sprite circulo;

        public static RectTransform CrearLienzo(Transform padre, string nombre)
        {
            AsegurarEventSystem();

            var objeto = new GameObject(nombre, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            objeto.transform.SetParent(padre, false);
            objeto.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var escala = objeto.GetComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            return objeto.GetComponent<RectTransform>();
        }

        public static Image CrearImagen(RectTransform padre, string nombre, Sprite sprite, Color color)
        {
            var objeto = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            objeto.transform.SetParent(padre, false);
            var imagen = objeto.GetComponent<Image>();
            imagen.sprite = sprite;
            imagen.color = color;
            return imagen;
        }

        public static TextMeshProUGUI CrearTexto(
            RectTransform padre, string nombre, float tamano, FontStyles estilo, Color color, Vector2 posicion, Vector2 medida)
        {
            var objeto = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
            objeto.transform.SetParent(padre, false);
            var texto = objeto.GetComponent<TextMeshProUGUI>();
            texto.fontSize = tamano;
            texto.fontStyle = estilo;
            texto.color = color;
            texto.alignment = TextAlignmentOptions.Center;
            texto.richText = true;
            texto.raycastTarget = false;
            Colocar(texto.rectTransform, posicion, medida);
            return texto;
        }

        public static Button CrearBoton(
            RectTransform padre, string texto, Vector2 posicion, Vector2 medida, Color color, UnityAction alPulsar)
        {
            var imagen = CrearImagen(padre, "Boton " + texto, null, color);
            Colocar(imagen.rectTransform, posicion, medida);

            var boton = imagen.gameObject.AddComponent<Button>();
            boton.targetGraphic = imagen;
            var colores = boton.colors;
            colores.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            boton.colors = colores;
            if (alPulsar != null) boton.onClick.AddListener(alPulsar);

            var etiqueta = CrearTexto(imagen.rectTransform, "Etiqueta", 44, FontStyles.Bold, Color.white, Vector2.zero, medida);
            etiqueta.text = texto;
            return boton;
        }

        public static void Colocar(RectTransform rt, Vector2 posicion, Vector2 medida)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = posicion;
            rt.sizeDelta = medida;
        }

        public static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Color ParseColor(string hex, Color porDefecto) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var color) ? color : porDefecto;

        /// <summary>Círculo blanco con borde suave; se tiñe con el color de la imagen.</summary>
        public static Sprite Circulo()
        {
            if (circulo != null) return circulo;

            const int tamano = 128;
            var textura = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var radio = tamano / 2f;
            var pixeles = new Color[tamano * tamano];
            for (var y = 0; y < tamano; y++)
            {
                for (var x = 0; x < tamano; x++)
                {
                    var distancia = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radio, radio));
                    pixeles[y * tamano + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radio - distancia));
                }
            }
            textura.SetPixels(pixeles);
            textura.Apply();
            circulo = Sprite.Create(textura, new Rect(0, 0, tamano, tamano), new Vector2(0.5f, 0.5f), 100f);
            return circulo;
        }

        /// <summary>Los botones necesitan un EventSystem; si la escena no trae uno, se crea con el módulo de entrada correcto.</summary>
        static void AsegurarEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var objeto = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            objeto.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            objeto.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
