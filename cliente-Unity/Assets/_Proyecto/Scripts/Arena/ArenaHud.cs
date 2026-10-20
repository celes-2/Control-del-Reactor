using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Interfaz sobre la arena: temporizador (CA-09.9), estado de la partida (CA-09.7),
    /// marcador (CA-15.2) y avisos.
    /// Si en el Inspector se asignan los cuatro textos (de un Canvas diseñado a mano),
    /// se usan esos. Si se dejan vacíos, se genera un HUD básico por código.
    /// </summary>
    public sealed class ArenaHud : MonoBehaviour
    {
        const float DuracionAviso = 3f;

        [Header("Opcional: textos de un Canvas diseñado en la escena")]
        [SerializeField] TextMeshProUGUI temporizador;
        [SerializeField] TextMeshProUGUI estado;
        [SerializeField] TextMeshProUGUI marcador;
        [SerializeField] TextMeshProUGUI aviso;

        [Tooltip("Objeto que se enciende/apaga con el aviso (por ejemplo, un panel con fondo). Si está vacío se usa el texto.")]
        [SerializeField] GameObject panelAviso;

        Coroutine rutinaAviso;

        GameObject ContenedorAviso => panelAviso != null ? panelAviso : aviso.gameObject;

        void Awake()
        {
            if (temporizador != null && estado != null && marcador != null && aviso != null)
            {
                ContenedorAviso.SetActive(false);
                return;
            }

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var escalador = canvasGo.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.matchWidthOrHeight = 0.5f;

            var raiz = canvasGo.transform;
            temporizador = CrearTexto(raiz, "Temporizador", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 130f), 60, TextAlignmentOptions.Center);
            estado = CrearTexto(raiz, "Estado", new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(650f, 60f), 34, TextAlignmentOptions.TopLeft);
            marcador = CrearTexto(raiz, "Marcador", new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(520f, 320f), 34, TextAlignmentOptions.TopRight);
            aviso = CrearTexto(raiz, "Aviso", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 140f), 72, TextAlignmentOptions.Center);
            aviso.color = new Color(1f, 0.92f, 0.3f);
            ContenedorAviso.SetActive(false);
        }

        public void MostrarTemporizador(string texto) => temporizador.text = texto;
        public void MostrarEstado(string texto) => estado.text = texto;
        public void MostrarMarcador(string texto) => marcador.text = texto;

        public void MostrarAviso(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            if (rutinaAviso != null) StopCoroutine(rutinaAviso);
            rutinaAviso = StartCoroutine(RutinaAviso(texto));
        }

        IEnumerator RutinaAviso(string texto)
        {
            aviso.text = texto;
            ContenedorAviso.SetActive(true);
            yield return new WaitForSeconds(DuracionAviso);
            ContenedorAviso.SetActive(false);
            rutinaAviso = null;
        }

        static TextMeshProUGUI CrearTexto(Transform padre, string nombre, Vector2 ancla, Vector2 posicion, Vector2 tamano, float fuente, TextAlignmentOptions alineacion)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = ancla;
            rt.anchorMax = ancla;
            rt.pivot = ancla;
            rt.anchoredPosition = posicion;
            rt.sizeDelta = tamano;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fuente;
            tmp.alignment = alineacion;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            return tmp;
        }
    }
}
