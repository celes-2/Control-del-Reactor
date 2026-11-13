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
        RectTransform podio;
        TextMeshProUGUI estadisticas;
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
            titulo.color = ConstructorUI.Arena;
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

            // Avatar propio: se pide SIEMPRE (haya o no imagen de reserva) y reemplaza lo que se vea al llegar.
            AvatarImagenes.CargarPropio(ganador.AvatarId, sprite =>
            {
                if (avatar == null) return;
                avatar.sprite = sprite;
                avatar.color = Color.white;
                avatar.preserveAspect = true;
                inicial.text = "";
            });
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
            // El avatar grande del ganador se reemplaza por el podio (US-26): 1.º, 2.º y 3.º, solo los que existan.
            anillo.gameObject.SetActive(false);
            ConstruirPodio(jugadores);

            // Del 4.º lugar en adelante (partidas de 4 jugadores) va en una línea de texto.
            var texto = new StringBuilder();
            for (var i = 3; i < jugadores.Count; i++)
            {
                var j = jugadores[i];
                var color = ColorUtility.ToHtmlStringRGB(ConstructorUI.ParseColor(j.Color, Color.white));
                var nombre = string.IsNullOrEmpty(j.Nombre) ? "Jugador" : j.Nombre;
                texto.AppendLine($"{i + 1}.  <color=#{color}>{nombre}</color>   {j.Puntaje} pts");
            }
            puntajes.text = texto.ToString();
        }

        public void MostrarEstadisticas(string texto) => estadisticas.text = texto ?? "";

        // ---------- Podio ----------

        static readonly Color Plata = new Color(0.80f, 0.82f, 0.88f);
        static readonly Color Bronce = new Color(0.80f, 0.52f, 0.28f);
        static readonly Color TextoOscuro = new Color(0.08f, 0.10f, 0.15f);

        void ConstruirPodio(IReadOnlyList<JugadorResultado> jugadores)
        {
            foreach (Transform hijo in podio) Destroy(hijo.gameObject);

            // 1.º al centro (el más alto), 2.º a la izquierda y 3.º a la derecha.
            float[] altos = { 280f, 220f, 180f };
            float[] xs = { 0f, -400f, 400f };
            Color[] metales = { ConstructorUI.Dorado, Plata, Bronce };
            const float baseY = -300f;

            var cantidad = Mathf.Min(3, jugadores.Count);
            for (var lugar = 0; lugar < cantidad; lugar++)
            {
                var j = jugadores[lugar];
                var alto = altos[lugar];
                var x = xs[lugar];

                var bloque = ConstructorUI.CrearImagen(podio, "Bloque" + (lugar + 1), null, metales[lugar]);
                ConstructorUI.Colocar(bloque.rectTransform, new Vector2(x, baseY + alto / 2f), new Vector2(340f, alto));
                ConstructorUI.CrearTexto(bloque.rectTransform, "Lugar", 72, FontStyles.Bold, TextoOscuro,
                    new Vector2(0f, alto / 2f - 45f), new Vector2(300f, 80f)).text = (lugar + 1).ToString();
                ConstructorUI.CrearTexto(bloque.rectTransform, "Nombre", 40, FontStyles.Bold, TextoOscuro,
                    new Vector2(0f, alto / 2f - 105f), new Vector2(320f, 50f)).text = string.IsNullOrEmpty(j.Nombre) ? "Jugador" : j.Nombre;
                ConstructorUI.CrearTexto(bloque.rectTransform, "Puntos", 36, FontStyles.Normal, TextoOscuro,
                    new Vector2(0f, alto / 2f - 150f), new Vector2(320f, 44f)).text = j.Puntaje + " pts";

                // Avatar sobre el bloque: aro con el color del jugador, imagen (o inicial) y avatar propio al llegar.
                var color = ConstructorUI.ParseColor(j.Color, Color.white);
                var aro = ConstructorUI.CrearImagen(podio, "AroPodio" + (lugar + 1), circulo, color);
                ConstructorUI.Colocar(aro.rectTransform, new Vector2(x, baseY + alto + 105f), new Vector2(190f, 190f));
                var foto = ConstructorUI.CrearImagen(aro.rectTransform, "Avatar", circulo, ConstructorUI.Panel);
                ConstructorUI.Colocar(foto.rectTransform, Vector2.zero, new Vector2(160f, 160f));
                var letra = ConstructorUI.CrearTexto(foto.rectTransform, "Inicial", 90, FontStyles.Bold, color, Vector2.zero, new Vector2(160f, 160f));

                var imagen = ImagenDe(j);
                if (imagen != null)
                {
                    foto.sprite = imagen; foto.color = Color.white; foto.preserveAspect = true;
                }
                else
                {
                    letra.text = string.IsNullOrEmpty(j.Nombre) ? "?" : j.Nombre.Substring(0, 1).ToUpperInvariant();
                }

                AvatarImagenes.CargarPropio(j.AvatarId, sprite =>
                {
                    if (foto == null) return;
                    foto.sprite = sprite; foto.color = Color.white; foto.preserveAspect = true;
                    letra.text = "";
                });
            }
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
            estadisticas.text = "";
            foreach (Transform hijo in podio) Destroy(hijo.gameObject);
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

            subtitulo = ConstructorUI.CrearTexto(raiz, "Subtitulo", 56, FontStyles.Bold, Color.white, new Vector2(0f, 270f), new Vector2(1500f, 90f));

            podio = new GameObject("Podio", typeof(RectTransform)).GetComponent<RectTransform>();
            podio.SetParent(raiz, false);
            ConstructorUI.Estirar(podio);

            estadisticas = ConstructorUI.CrearTexto(raiz, "Estadisticas", 34, FontStyles.Normal, ConstructorUI.Gris, new Vector2(0f, -345f), new Vector2(1700f, 50f));
            puntajes = ConstructorUI.CrearTexto(raiz, "Puntajes", 36, FontStyles.Normal, Color.white, new Vector2(0f, -400f), new Vector2(1200f, 60f));

            ConstructorUI.CrearBoton(raiz, "Volver", new Vector2(0f, -490f), new Vector2(420f, 90f), ConstructorUI.Azul,
                () => VolverPulsado?.Invoke());
        }

        // ---------- Utilidades ----------

        Sprite ImagenDe(JugadorResultado jugador)
        {
            var img = AvatarImagenes.DePredeterminado(jugador.AvatarId);
            if (img != null) return img;

            if (spritesAvatar == null || spritesAvatar.Length == 0) return null;
            return spritesAvatar[jugador.OrdenLlegada % spritesAvatar.Length];
        }
    }
}
