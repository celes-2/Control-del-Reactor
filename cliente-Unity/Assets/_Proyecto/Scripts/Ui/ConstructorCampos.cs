using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Ui
{
    /// <summary>Campos de texto armados por código (complementa a ConstructorUI).</summary>
    public static class ConstructorCampos
    {
        static readonly Color FondoCampo = new Color(0.18f, 0.21f, 0.30f, 1f);

        public static TMP_InputField CrearCampo(
            RectTransform padre, string nombre, string indicacion, Vector2 posicion, Vector2 medida, bool contrasena = false)
        {
            var fondo = ConstructorUI.CrearImagen(padre, "Campo " + nombre, null, FondoCampo);
            ConstructorUI.Colocar(fondo.rectTransform, posicion, medida);

            var area = new GameObject("Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(fondo.rectTransform, false);
            ConstructorUI.Estirar(area);
            area.offsetMin = new Vector2(24f, 6f);
            area.offsetMax = new Vector2(-24f, -6f);

            var texto = ConstructorUI.CrearTexto(area, "Texto", 40, FontStyles.Normal, Color.white, Vector2.zero, medida);
            texto.alignment = TextAlignmentOptions.MidlineLeft;
            texto.richText = false;
            ConstructorUI.Estirar(texto.rectTransform);

            var marca = ConstructorUI.CrearTexto(area, "Indicacion", 40, FontStyles.Italic, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, medida);
            marca.alignment = TextAlignmentOptions.MidlineLeft;
            marca.text = indicacion;
            ConstructorUI.Estirar(marca.rectTransform);

            var campo = fondo.gameObject.AddComponent<TMP_InputField>();
            campo.targetGraphic = fondo;
            campo.textViewport = area;
            campo.textComponent = texto;
            campo.placeholder = marca;
            campo.lineType = TMP_InputField.LineType.SingleLine;
            campo.contentType = contrasena ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            campo.characterLimit = 120;
            campo.pointSize = 40;
            return campo;
        }

        /// <summary>Botón de texto sin relleno, para enlaces como "Crear cuenta".</summary>
        public static Button CrearEnlace(
            RectTransform padre, string texto, Vector2 posicion, Vector2 medida, UnityEngine.Events.UnityAction alPulsar)
        {
            var boton = ConstructorUI.CrearBoton(padre, texto, posicion, medida, new Color(1f, 1f, 1f, 0f), alPulsar);
            var etiqueta = boton.GetComponentInChildren<TextMeshProUGUI>();
            etiqueta.fontSize = 36;
            etiqueta.fontStyle = FontStyles.Underline;
            etiqueta.color = new Color(0.62f, 0.78f, 1f, 1f);
            return boton;
        }
    }
}
