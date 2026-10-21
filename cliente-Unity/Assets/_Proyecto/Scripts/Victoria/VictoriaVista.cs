using System;
using System.Collections.Generic;
using System.Text;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Victoria
{
    /// <summary>
    /// Vista de la escena Victoria (MVP, US-17). Construye su interfaz por código, así que solo hace
    /// falta agregar este componente (y VictoriaInstalador) a un objeto vacío de la escena.
    /// Muestra el avatar ganador, los puntajes, un mensaje festivo, confeti y música.
    /// </summary>
    public sealed class VictoriaVista : MonoBehaviour, IVictoriaVista
    {
        [Header("Opcional: imágenes de los avatares")]
        [Tooltip("Usar la MISMA lista, en el MISMO orden, que en ArenaVista. Vacío = círculo con la inicial.")]
        [SerializeField] Sprite[] spritesAvatar = new Sprite[0];

        [Header("Opcional: música de victoria")]
        [Tooltip("Se reproduce en bucle al celebrar. Mientras no exista el AudioService, se pone aquí (CA-17.4).")]
        [SerializeField] AudioClip musicaVictoria;
        [SerializeField, Range(0f, 1f)] float volumenMusica = 0.6f;

        [Header("Confeti")]
        [SerializeField, Range(0, 300)] int piezasDeConfeti = 140;

        static readonly Color[] ColoresConfeti =
        {
            new Color(0.91f, 0.30f, 0.24f), new Color(0.20f, 0.60f, 0.86f), new Color(0.18f, 0.80f, 0.44f),
            new Color(0.95f, 0.77f, 0.06f), new Color(0.61f, 0.35f, 0.71f), new Color(1f, 0.5f, 0.1f),
        };

        public event Action VolverPulsado;

        RectTransform raiz;
        RectTransform areaConfeti;
        TextMeshProUGUI titulo;
        TextMeshProUGUI subtitulo;
        TextMeshProUGUI puntajes;
        Image anillo;
        Image avatar;
        TextMeshProUGUI inicial;
        Sprite circulo;
        ConfetiUI confeti;
        AudioSource audioFuente;

        void Awake()
        {
            circulo = ConstructorUI.Circulo();
            ConstruirInterfaz();
        }

        // ---------- IVictoriaVista ----------

        public void MostrarGanador(JugadorResultado ganador, string textoTitulo, string textoSubtitulo)
        {
            titulo.text = textoTitulo;
            titulo.color = ConstructorUI.Dorado;
            subtitulo.text = textoSubtitulo;

            var color = ConstructorUI.ParseColor(ganador.Color, Color.white);
            anillo.gameObject.SetActive(true);
            anillo.color = color;

            var imagen = ImagenDe(ganador);
            if (imagen != null)
            {
                avatar.sprite = imagen;
                avatar.color = Color.white;
                avatar.preserveAspect = true;
                inicial.text = "";
            }
            else
            {
                avatar.sprite = circulo;
                avatar.color = ConstructorUI.Panel;
                inicial.text = string.IsNullOrEmpty(ganador.Nombre) ? "?" : ganador.Nombre.Substring(0, 1).ToUpperInvariant();
                inicial.color = color;
            }
        }

        public void MostrarSinGanador(string textoTitulo, string textoSubtitulo)
        {
            titulo.text = textoTitulo;
            titulo.color = Color.white;
            subtitulo.text = textoSubtitulo;
            anillo.gameObject.SetActive(false);
        }

        public void MostrarPuntajes(IReadOnlyList<JugadorResultado> jugadores)
        {
            var texto = new StringBuilder();
            for (var i = 0; i < jugadores.Count; i++)
            {
                var j = jugadores[i];
                var color = ColorUtility.ToHtmlStringRGB(ConstructorUI.ParseColor(j.Color, Color.white));
                var nombre = string.IsNullOrEmpty(j.Nombre) ? "Jugador" : j.Nombre;
                var linea = $"{i + 1}.  <color=#{color}>{nombre}</color>   {j.Puntaje} pts";
                texto.AppendLine(j.EsGanador ? $"<b>{linea}</b>" : linea);
            }
            puntajes.text = texto.ToString();
        }

        public void Celebrar()
        {
            confeti.Iniciar(areaConfeti, piezasDeConfeti, ColoresConfeti);

            if (musicaVictoria == null) return;
            if (audioFuente == null) audioFuente = gameObject.AddComponent<AudioSource>();
            audioFuente.clip = musicaVictoria;
            audioFuente.loop = true;
            audioFuente.volume = volumenMusica;
            audioFuente.Play();
        }

        public void MostrarSinResultado(string texto)
        {
            titulo.text = "";
            anillo.gameObject.SetActive(false);
            subtitulo.text = texto;
            puntajes.text = "";
        }

        // ---------- Construcción de la interfaz ----------

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "VictoriaUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            areaConfeti = new GameObject("Confeti", typeof(RectTransform)).GetComponent<RectTransform>();
            areaConfeti.SetParent(raiz, false);
            ConstructorUI.Estirar(areaConfeti);
            confeti = areaConfeti.gameObject.AddComponent<ConfetiUI>();

            titulo = ConstructorUI.CrearTexto(raiz, "Titulo", 110, FontStyles.Bold, ConstructorUI.Arena, new Vector2(0f, 390f), new Vector2(1500f, 140f));

            anillo = ConstructorUI.CrearImagen(raiz, "Anillo", circulo, Color.white);
            ConstructorUI.Colocar(anillo.rectTransform, new Vector2(0f, 130f), new Vector2(320f, 320f));
            avatar = ConstructorUI.CrearImagen(anillo.rectTransform, "Avatar", circulo, ConstructorUI.Panel);
            ConstructorUI.Colocar(avatar.rectTransform, Vector2.zero, new Vector2(270f, 270f));
            inicial = ConstructorUI.CrearTexto(avatar.rectTransform, "Inicial", 150, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(270f, 270f));

            subtitulo = ConstructorUI.CrearTexto(raiz, "Subtitulo", 56, FontStyles.Bold, Color.white, new Vector2(0f, -110f), new Vector2(1500f, 90f));
            puntajes = ConstructorUI.CrearTexto(raiz, "Puntajes", 46, FontStyles.Normal, Color.white, new Vector2(0f, -290f), new Vector2(900f, 220f));
            puntajes.alignment = TextAlignmentOptions.Top;

            ConstructorUI.CrearBoton(raiz, "Volver", new Vector2(0f, -460f), new Vector2(420f, 90f), ConstructorUI.Azul,
                () => VolverPulsado?.Invoke());
        }

        // ---------- Utilidades ----------

        Sprite ImagenDe(JugadorResultado jugador)
        {
            if (spritesAvatar == null || spritesAvatar.Length == 0) return null;
            return spritesAvatar[jugador.OrdenLlegada % spritesAvatar.Length];
        }
    }
}
