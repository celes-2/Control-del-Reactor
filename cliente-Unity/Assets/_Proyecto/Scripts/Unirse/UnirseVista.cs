using System;
using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;

namespace Reactor.Cliente.Unirse
{
    /// <summary>
    /// Vista de la escena Unirse (MVP). Construye su interfaz por código: solo hace falta agregar este
    /// componente (y UnirseInstalador) a un objeto vacío de la escena. Actualiza la lista sola cada pocos segundos.
    /// </summary>
    public sealed class UnirseVista : MonoBehaviour, IUnirseVista
    {
        const int MaxFilas = 6;

        [SerializeField, Tooltip("Cada cuántos segundos se actualiza la lista sola. 0 = nunca.")] float segundosRefresco = 3f;
        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse.")] float segundosAviso = 4f;

        public event Action<string> UnirsePulsado;
        public event Action ActualizarPulsado;
        public event Action VolverPulsado;

        RectTransform raiz;
        RectTransform contenedorFilas;
        TextMeshProUGUI estado, aviso;
        Coroutine borrarAviso;

        void Awake() => ConstruirInterfaz();

        void OnEnable()
        {
            if (segundosRefresco > 0f) StartCoroutine(RefrescarSolo());
        }

        IEnumerator RefrescarSolo()
        {
            var espera = new WaitForSeconds(segundosRefresco);
            while (true)
            {
                yield return espera;
                ActualizarPulsado?.Invoke();
            }
        }

        public void MostrarEstado(string texto) => estado.text = texto;

        public void MostrarPartidas(IReadOnlyList<PartidaAbierta> partidas)
        {
            for (var i = contenedorFilas.childCount - 1; i >= 0; i--)
                Destroy(contenedorFilas.GetChild(i).gameObject);

            for (var i = 0; i < partidas.Count && i < MaxFilas; i++)
            {
                var partida = partidas[i];
                var y = -i * 90f;

                var texto = ConstructorUI.CrearTexto(contenedorFilas, "Partida", 44, FontStyles.Bold, Color.white,
                    new Vector2(-280f, y), new Vector2(900f, 80f));
                texto.alignment = TextAlignmentOptions.Left;
                texto.text = $"{(string.IsNullOrEmpty(partida.Anfitrion) ? "Partida" : partida.Anfitrion)}"
                             + $"   <size=70%><color=#9EA8BD>{partida.Jugadores}/{partida.MaxJugadores} jugadores</color></size>";

                var id = partida.Id;
                var boton = ConstructorUI.CrearBoton(contenedorFilas, partida.EstaLlena ? "Llena" : "Unirse",
                    new Vector2(520f, y), new Vector2(240f, 76f),
                    partida.EstaLlena ? ConstructorUI.Gris : ConstructorUI.Verde, () => UnirsePulsado?.Invoke(id));
                boton.interactable = !partida.EstaLlena;
            }
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
            raiz = ConstructorUI.CrearLienzo(transform, "UnirseUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            ConstructorUI.CrearTexto(raiz, "Titulo", 100, FontStyles.Bold, ConstructorUI.Dorado,
                new Vector2(0f, 400f), new Vector2(1500f, 130f)).text = "PARTIDAS ABIERTAS";

            estado = ConstructorUI.CrearTexto(raiz, "Estado", 48, FontStyles.Bold, Color.white,
                new Vector2(0f, 300f), new Vector2(1500f, 80f));
            estado.text = "Buscando partidas...";

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, 30f), new Vector2(1500f, 560f));

            contenedorFilas = new GameObject("Partidas", typeof(RectTransform)).GetComponent<RectTransform>();
            contenedorFilas.SetParent(raiz, false);
            ConstructorUI.Colocar(contenedorFilas, new Vector2(0f, 220f), new Vector2(1400f, 500f));

            ConstructorUI.CrearBoton(raiz, "Actualizar", new Vector2(-290f, -340f), new Vector2(520f, 100f),
                ConstructorUI.Azul, () => ActualizarPulsado?.Invoke());
            ConstructorUI.CrearBoton(raiz, "Volver", new Vector2(290f, -340f), new Vector2(520f, 100f),
                ConstructorUI.Gris, () => VolverPulsado?.Invoke());

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 40, FontStyles.Bold, new Color(1f, 0.55f, 0.45f),
                new Vector2(0f, -440f), new Vector2(1500f, 70f));
        }
    }
}
