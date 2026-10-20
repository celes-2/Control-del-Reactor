using System.Collections.Generic;
using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Fondo de estrellas que parpadean. Se coloca en la Main Camera de la escena Arena.
    /// Las estrellas son hijas de la cámara, así que siempre cubren lo que se ve,
    /// y se regeneran solas si cambia el zoom (cuando el ArenaRenderer ajusta la cámara).
    /// Es solo decoración: no tiene nada que ver con la lógica del juego.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FondoEstrellas : MonoBehaviour
    {
        [Header("Estrellas")]
        [SerializeField, Range(10, 1000)] int cantidad = 180;
        [Tooltip("Tamaño mínimo y máximo de cada estrella, en unidades (1 = una celda).")]
        [SerializeField] Vector2 tamano = new Vector2(0.03f, 0.10f);
        [Tooltip("Brillo mínimo y máximo (transparencia).")]
        [SerializeField] Vector2 brillo = new Vector2(0.35f, 1f);
        [SerializeField] Color color = Color.white;

        [Header("Parpadeo")]
        [SerializeField, Range(0f, 5f)] float velocidadParpadeo = 1.5f;
        [Tooltip("0 = no parpadean; 1 = se apagan por completo.")]
        [SerializeField, Range(0f, 1f)] float intensidadParpadeo = 0.5f;

        [Header("Otros")]
        [Tooltip("Debe ser menor que el de los tiles (0) para quedar detrás del mapa.")]
        [SerializeField] int ordenDibujo = -100;
        [Tooltip("0 = posiciones distintas cada vez. Otro número = siempre el mismo cielo.")]
        [SerializeField] int semilla = 0;

        struct Estrella
        {
            public SpriteRenderer Renderer;
            public float AlfaBase;
            public float Fase;
            public float Velocidad;
        }

        readonly List<Estrella> estrellas = new List<Estrella>();
        Camera cam;
        Transform contenedor;
        float tamanoGenerado = -1f;
        float aspectoGenerado = -1f;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (!cam.orthographic) return;

            if (!Mathf.Approximately(cam.orthographicSize, tamanoGenerado) || !Mathf.Approximately(cam.aspect, aspectoGenerado))
                Generar();

            float t = Time.time;
            foreach (var e in estrellas)
            {
                float onda = 0.5f + 0.5f * Mathf.Sin(t * e.Velocidad + e.Fase);   // 0..1
                var c = e.Renderer.color;
                c.a = e.AlfaBase * (1f - intensidadParpadeo * onda);
                e.Renderer.color = c;
            }
        }

        void OnValidate()
        {
            // Al cambiar valores en el Inspector durante el Play, se regenera el cielo.
            tamanoGenerado = -1f;
        }

        void Generar()
        {
            if (contenedor != null) Destroy(contenedor.gameObject);
            estrellas.Clear();

            contenedor = new GameObject("Estrellas").transform;
            contenedor.SetParent(transform, false);
            contenedor.localPosition = new Vector3(0f, 0f, 10f); // delante de la cámara, detrás de todo por el orden de dibujo

            tamanoGenerado = cam.orthographicSize;
            aspectoGenerado = cam.aspect;

            float mitadAlto = cam.orthographicSize * 1.05f;
            float mitadAncho = mitadAlto * cam.aspect;

            var azar = semilla == 0 ? new System.Random() : new System.Random(semilla);
            var sprite = GeneradorSprites.Circulo();

            for (int i = 0; i < cantidad; i++)
            {
                var go = new GameObject("Estrella");
                go.transform.SetParent(contenedor, false);
                go.transform.localPosition = new Vector3(Rango(azar, -mitadAncho, mitadAncho), Rango(azar, -mitadAlto, mitadAlto), 0f);
                go.transform.localScale = Vector3.one * Rango(azar, tamano.x, tamano.y);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = ordenDibujo;

                float alfa = Rango(azar, brillo.x, brillo.y);
                sr.color = new Color(color.r, color.g, color.b, alfa);

                estrellas.Add(new Estrella
                {
                    Renderer = sr,
                    AlfaBase = alfa,
                    Fase = Rango(azar, 0f, Mathf.PI * 2f),
                    Velocidad = velocidadParpadeo * Rango(azar, 0.5f, 1.5f),
                });
            }
        }

        static float Rango(System.Random azar, float min, float max) => min + (float)azar.NextDouble() * (max - min);
    }
}
