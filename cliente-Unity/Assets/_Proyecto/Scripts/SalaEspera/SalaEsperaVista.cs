using System;
using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Reactor.Cliente.SalaEspera
{
    /// <summary>
    /// Vista de la escena Sala de espera (MVP, US-09). Construye su interfaz por código: solo hace falta
    /// agregar este componente (y SalaEsperaInstalador) a un objeto vacío de la escena.
    /// </summary>
    public sealed class SalaEsperaVista : MonoBehaviour, ISalaEsperaVista
    {
        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse.")] float segundosAviso = 4f;

        public event Action IniciarPulsado;

        RectTransform raiz;
        TextMeshProUGUI codigo;
        TextMeshProUGUI estado;
        TextMeshProUGUI aviso;
        Button botonIniciar;
        RectTransform contenedorFilas;
        Coroutine borrarAviso;

        void Awake() => ConstruirInterfaz();

        public void MostrarCodigoPartida(string partidaId) => codigo.text = "Partida: " + partidaId;

        public void MostrarEstado(string texto) => estado.text = texto;

        public void MostrarJugadores(IReadOnlyList<JugadorMsg> jugadores, string anfitrionId, string usuarioId)
        
        {
            foreach (var j in jugadores) Debug.Log($"Jugador {j.Nombre} avatarId='{j.AvatarId}'");
            
            for (var i = contenedorFilas.childCount - 1; i >= 0; i--)
                Destroy(contenedorFilas.GetChild(i).gameObject);

            for (var i = 0; i < jugadores.Count; i++)
            {
                var jugador = jugadores[i];
                var color = ConstructorUI.ParseColor(jugador.Color, Color.white);
                var y = -i * 90f;

                // Imagen del avatar predeterminado; si no hay, el círculo de color de antes.
                var imagenAvatar = AvatarImagenes.DePredeterminado(jugador.AvatarId);
                var punto = imagenAvatar != null
                    ? ConstructorUI.CrearImagen(contenedorFilas, "Avatar", imagenAvatar, Color.white)
                    : ConstructorUI.CrearImagen(contenedorFilas, "Color", ConstructorUI.Circulo(), color);
                punto.preserveAspect = true;
                ConstructorUI.Colocar(punto.rectTransform, new Vector2(-300f, y), new Vector2(72f, 72f));

                var etiquetas = "";
                if (jugador.Id == anfitrionId) etiquetas += "   <size=60%><color=#FFD133>ANFITRIÓN</color></size>";
                if (!string.IsNullOrEmpty(usuarioId) && jugador.Id == usuarioId) etiquetas += "   <size=60%><color=#9EA8BD>(tú)</color></size>";

                var nombre = ConstructorUI.CrearTexto(contenedorFilas, "Nombre", 52, FontStyles.Bold, Color.white,
                    new Vector2(60f, y), new Vector2(560f, 70f));
                nombre.alignment = TextAlignmentOptions.Left;
                nombre.text = (string.IsNullOrEmpty(jugador.Nombre) ? "Jugador" : jugador.Nombre) + etiquetas;
            }
        }

        public void ConfigurarBotonIniciar(bool visible, bool habilitado)
        {
            botonIniciar.gameObject.SetActive(visible);
            botonIniciar.interactable = habilitado;
        }

        public void MostrarAviso(string texto)
        {
            aviso.text = texto;
            if (borrarAviso != null) StopCoroutine(borrarAviso);
            if (!string.IsNullOrEmpty(texto)) borrarAviso = StartCoroutine(BorrarAvisoLuego());
        }

        IEnumerator BorrarAvisoLuego()
        {
            yield return new WaitForSeconds(segundosAviso);
            aviso.text = "";
            borrarAviso = null;
        }

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "SalaEsperaUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            ConstructorUI.CrearTexto(raiz, "Titulo", 100, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 400f), new Vector2(1500f, 130f)).text = "SALA DE ESPERA";

            codigo = ConstructorUI.CrearTexto(raiz, "Codigo", 36, FontStyles.Normal, ConstructorUI.Gris,
                new Vector2(0f, 320f), new Vector2(1500f, 60f));

            estado = ConstructorUI.CrearTexto(raiz, "Estado", 64, FontStyles.Bold, Color.white,
                new Vector2(0f, 200f), new Vector2(1500f, 100f));

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, -40f), new Vector2(900f, 380f));

            contenedorFilas = new GameObject("Jugadores", typeof(RectTransform)).GetComponent<RectTransform>();
            contenedorFilas.SetParent(raiz, false);
            ConstructorUI.Colocar(contenedorFilas, new Vector2(0f, 70f), new Vector2(900f, 380f));

            botonIniciar = ConstructorUI.CrearBoton(raiz, "Iniciar partida", new Vector2(0f, -330f), new Vector2(520f, 100f),
                ConstructorUI.Verde, () => IniciarPulsado?.Invoke());

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 40, FontStyles.Bold, new Color(1f, 0.55f, 0.45f),
                new Vector2(0f, -450f), new Vector2(1500f, 70f));
        }
    }
}
