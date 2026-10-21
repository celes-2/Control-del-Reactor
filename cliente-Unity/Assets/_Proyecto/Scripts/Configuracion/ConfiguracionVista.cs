using System;
using System.Collections;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Configuracion
{
    /// <summary>
    /// Vista de la escena Configuración (MVP, US-08). Construye su interfaz por código: solo hace falta
    /// agregar este componente (y ConfiguracionInstalador) a un objeto vacío de la escena.
    /// Para cambiar textos, colores o posiciones, editar ConstruirInterfaz.
    /// </summary>
    public sealed class ConfiguracionVista : MonoBehaviour, IConfiguracionVista
    {
        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse.")] float segundosAviso = 4f;

        static readonly Color Apagado = new Color(0.22f, 0.26f, 0.36f, 1f);

        public event Action<CampoAjuste, int> AjusteCambiado;
        public event Action<ModoFin> ModoElegido;
        public event Action CrearPulsado;
        public event Action UnirsePulsado;

        RectTransform raiz;
        Image botonTiempo, botonPuntos;
        TextMeshProUGUI etiquetaValor, valor, congelamiento, jugadores, aviso;
        Button botonCrear;
        Coroutine borrarAviso;

        void Awake() => ConstruirInterfaz();

        public void MostrarModo(ModoFin modo)
        {
            botonTiempo.color = modo == ModoFin.Tiempo ? ConstructorUI.Azul : Apagado;
            botonPuntos.color = modo == ModoFin.Puntos ? ConstructorUI.Azul : Apagado;
        }

        public void MostrarValor(string etiqueta, string texto)
        {
            etiquetaValor.text = etiqueta;
            valor.text = texto;
        }

        public void MostrarCongelamiento(string texto) => congelamiento.text = texto;

        public void MostrarJugadores(string texto) => jugadores.text = texto;

        public void BloquearCrear(bool bloqueado) => botonCrear.interactable = !bloqueado;

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
            raiz = ConstructorUI.CrearLienzo(transform, "ConfiguracionUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            ConstructorUI.CrearTexto(raiz, "Titulo", 100, FontStyles.Bold, ConstructorUI.Dorado,
                new Vector2(0f, 400f), new Vector2(1500f, 130f)).text = "CONFIGURACIÓN";

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, 60f), new Vector2(1500f, 560f));

            // Fila 1: cómo termina la partida.
            Etiqueta("Fin de la partida", 250f);
            botonTiempo = ConstructorUI.CrearBoton(raiz, "Por tiempo", new Vector2(130f, 250f), new Vector2(320f, 90f),
                Apagado, () => ModoElegido?.Invoke(ModoFin.Tiempo)).targetGraphic as Image;
            botonPuntos = ConstructorUI.CrearBoton(raiz, "Por puntos", new Vector2(480f, 250f), new Vector2(320f, 90f),
                Apagado, () => ModoElegido?.Invoke(ModoFin.Puntos)).targetGraphic as Image;

            // Fila 2: duración o puntos, según el modo.
            etiquetaValor = Etiqueta("Duración", 120f);
            valor = Selector(120f, CampoAjuste.Valor);

            // Fila 3: congelamiento.
            Etiqueta("Congelamiento", -10f);
            congelamiento = Selector(-10f, CampoAjuste.Congelamiento);

            // Fila 4: máximo de jugadores.
            Etiqueta("Máximo de jugadores", -140f);
            jugadores = Selector(-140f, CampoAjuste.Jugadores);

            botonCrear = ConstructorUI.CrearBoton(raiz, "Crear partida", new Vector2(-290f, -340f), new Vector2(520f, 100f),
                ConstructorUI.Verde, () => CrearPulsado?.Invoke());
            ConstructorUI.CrearBoton(raiz, "Unirse a una partida", new Vector2(290f, -340f), new Vector2(520f, 100f),
                ConstructorUI.Azul, () => UnirsePulsado?.Invoke());

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 40, FontStyles.Bold, new Color(1f, 0.55f, 0.45f),
                new Vector2(0f, -440f), new Vector2(1500f, 70f));
        }

        TextMeshProUGUI Etiqueta(string texto, float y)
        {
            var etiqueta = ConstructorUI.CrearTexto(raiz, "Etiqueta " + texto, 44, FontStyles.Bold, Color.white,
                new Vector2(-380f, y), new Vector2(600f, 80f));
            etiqueta.alignment = TextAlignmentOptions.Left;
            etiqueta.text = texto;
            return etiqueta;
        }

        /// <summary>Botón -, texto del valor y botón +.</summary>
        TextMeshProUGUI Selector(float y, CampoAjuste campo)
        {
            ConstructorUI.CrearBoton(raiz, "-", new Vector2(20f, y), new Vector2(90f, 90f),
                ConstructorUI.Azul, () => AjusteCambiado?.Invoke(campo, -1));
            var texto = ConstructorUI.CrearTexto(raiz, "Valor " + campo, 48, FontStyles.Bold, ConstructorUI.Dorado,
                new Vector2(270f, y), new Vector2(380f, 90f));
            ConstructorUI.CrearBoton(raiz, "+", new Vector2(520f, y), new Vector2(90f, 90f),
                ConstructorUI.Azul, () => AjusteCambiado?.Invoke(campo, 1));
            return texto;
        }
    }
}
