using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Victoria
{
    /// <summary>
    /// Confeti hecho con imágenes de interfaz que caen y se balancean. Se usa UI en lugar de
    /// partículas para no depender de materiales (con URP 2D un material faltante se ve rosado).
    /// </summary>
    public sealed class ConfetiUI : MonoBehaviour
    {
        struct Pieza
        {
            public RectTransform Rect;
            public float Velocidad;
            public float Fase;
            public float Balanceo;
            public float Giro;
        }

        Pieza[] piezas = new Pieza[0];
        RectTransform area;

        public void Iniciar(RectTransform areaDeCaida, int cantidad, Color[] colores)
        {
            area = areaDeCaida;
            Limpiar();
            if (cantidad <= 0 || colores == null || colores.Length == 0) return;

            var ancho = area.rect.width;
            var alto = area.rect.height;
            piezas = new Pieza[cantidad];

            for (var i = 0; i < cantidad; i++)
            {
                var objeto = new GameObject("Pieza", typeof(RectTransform), typeof(Image));
                objeto.transform.SetParent(area, false);

                var imagen = objeto.GetComponent<Image>();
                imagen.color = colores[Random.Range(0, colores.Length)];
                imagen.raycastTarget = false;

                var rt = imagen.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Random.Range(10f, 18f), Random.Range(18f, 30f));
                rt.anchoredPosition = new Vector2(
                    Random.Range(-ancho / 2f, ancho / 2f),
                    Random.Range(-alto / 2f, alto / 2f + 200f));
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                piezas[i] = new Pieza
                {
                    Rect = rt,
                    Velocidad = Random.Range(140f, 360f),
                    Fase = Random.Range(0f, 6.28f),
                    Balanceo = Random.Range(20f, 90f),
                    Giro = Random.Range(-220f, 220f),
                };
            }
        }

        void Update()
        {
            if (area == null || piezas.Length == 0) return;

            var dt = Time.unscaledDeltaTime;
            var ancho = area.rect.width;
            var alto = area.rect.height;

            for (var i = 0; i < piezas.Length; i++)
            {
                var p = piezas[i];
                var pos = p.Rect.anchoredPosition;
                pos.y -= p.Velocidad * dt;
                pos.x += Mathf.Sin(Time.unscaledTime * 2f + p.Fase) * p.Balanceo * dt;

                if (pos.y < -alto / 2f - 40f)
                {
                    pos.y = alto / 2f + 40f;
                    pos.x = Random.Range(-ancho / 2f, ancho / 2f);
                }

                p.Rect.anchoredPosition = pos;
                p.Rect.Rotate(0f, 0f, p.Giro * dt);
            }
        }

        void Limpiar()
        {
            foreach (var p in piezas)
                if (p.Rect != null) Destroy(p.Rect.gameObject);
            piezas = new Pieza[0];
        }
    }
}
