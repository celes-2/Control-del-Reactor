using System;
using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Perfil
{
    /// <summary>
    /// Vista de la escena Perfil (MVP, US-04 / US-06): muestra al jugador, los avatares del catálogo, su imagen propia
    /// y un campo para subir una imagen desde el disco. Construye su interfaz por código: solo hace falta agregar
    /// este componente (y PerfilInstalador) a un objeto vacío de la escena.
    /// </summary>
    public sealed class PerfilVista : MonoBehaviour, IPerfilVista
    {
        const int PorFila = 3;

        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse. 0 = no se borra.")] float segundosAviso = 6f;

        static readonly Color Exito = new Color(0.45f, 0.9f, 0.6f);
        static readonly Color Fallo = new Color(1f, 0.55f, 0.45f);

        public event Action<string> AvatarElegido;
        public event Action<string> SubirImagenPulsado;
        public event Action ContinuarPulsado;
        public event Action CerrarSesionPulsado;

        readonly Dictionary<string, Sprite> imagenes = new Dictionary<string, Sprite>();
        IReadOnlyList<AvatarInfo> ultimaLista = new List<AvatarInfo>();
        string ultimoSeleccionado;

        RectTransform raiz, contenedor;
        TextMeshProUGUI jugador, aviso;
        TMP_InputField campoRuta;
        Button botonContinuar, botonSubir;
        Coroutine borrarAviso;

        void Awake() => ConstruirInterfaz();

        public void MostrarJugador(string nombre, string correo) =>
            jugador.text = $"{nombre}\n<size=60%><color=#9EA8BD>{correo}</color></size>";

        public void MostrarAvatares(IReadOnlyList<AvatarInfo> avatares, string seleccionadoId)
        {
            ultimaLista = avatares;
            ultimoSeleccionado = seleccionadoId;
            Redibujar();
        }

        public void MostrarImagenAvatar(string avatarId, Sprite sprite)
        {
            imagenes[avatarId] = sprite;
            Redibujar();
        }

        void Redibujar()
        {
            for (var i = contenedor.childCount - 1; i >= 0; i--)
                Destroy(contenedor.GetChild(i).gameObject);

            for (var i = 0; i < ultimaLista.Count; i++)
            {
                var avatar = ultimaLista[i];
                var fila = i / PorFila;
                var columnasEnFila = Mathf.Min(PorFila, ultimaLista.Count - fila * PorFila);
                var columna = i % PorFila;
                var x = (columna - (columnasEnFila - 1) / 2f) * 330f;
                var y = -fila * 290f;

                var elegido = avatar.Id == ultimoSeleccionado;
                if (elegido)
                {
                    var aro = ConstructorUI.CrearImagen(contenedor, "Aro", ConstructorUI.Circulo(), ConstructorUI.Dorado);
                    ConstructorUI.Colocar(aro.rectTransform, new Vector2(x, y), new Vector2(220f, 220f));
                }

                Image imagen;
                if (avatar.Propio && imagenes.TryGetValue(avatar.Id, out var sprite) && sprite != null)
                {
                    imagen = ConstructorUI.CrearImagen(contenedor, "Avatar " + avatar.Nombre, sprite, Color.white);
                    imagen.preserveAspect = true;
                }
                else
                {
                    var color = ConstructorUI.ParseColor(avatar.Color, ConstructorUI.Gris);
                    var predet = AvatarImagenes.PorNombre(avatar.Nombre);
                    imagen = predet != null
                        ? ConstructorUI.CrearImagen(contenedor, "Avatar " + avatar.Nombre, predet, Color.white)
                        : ConstructorUI.CrearImagen(contenedor, "Avatar " + avatar.Nombre, ConstructorUI.Circulo(), color);
                    imagen.preserveAspect = true;
                }
                ConstructorUI.Colocar(imagen.rectTransform, new Vector2(x, y), new Vector2(190f, 190f));

                var boton = imagen.gameObject.AddComponent<Button>();
                boton.targetGraphic = imagen;
                var id = avatar.Id;
                boton.onClick.AddListener(() => AvatarElegido?.Invoke(id));

                var nombre = ConstructorUI.CrearTexto(contenedor, "Nombre", 34, elegido ? FontStyles.Bold : FontStyles.Normal,
                    elegido ? ConstructorUI.Dorado : Color.white, new Vector2(x, y - 125f), new Vector2(300f, 46f));
                nombre.text = avatar.Nombre;
            }
        }

        public void MostrarAviso(string texto, bool exito)
        {
            aviso.color = exito ? Exito : Fallo;
            aviso.text = texto;
            if (borrarAviso != null) StopCoroutine(borrarAviso);
            if (!string.IsNullOrEmpty(texto) && segundosAviso > 0f) borrarAviso = StartCoroutine(BorrarAvisoLuego());
        }

        public void BloquearContinuar(bool bloqueado) => botonContinuar.interactable = !bloqueado;
        public void BloquearSubida(bool bloqueado) => botonSubir.interactable = !bloqueado;
        public void LimpiarRuta() => campoRuta.text = "";

        IEnumerator BorrarAvisoLuego()
        {
            yield return new WaitForSeconds(segundosAviso);
            aviso.text = "";
            borrarAviso = null;
        }

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "PerfilUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            ConstructorUI.CrearTexto(raiz, "Titulo", 90, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 450f), new Vector2(1600f, 110f)).text = "ELIGE TU AVATAR";

            jugador = ConstructorUI.CrearTexto(raiz, "Jugador", 46, FontStyles.Bold, Color.white,
                new Vector2(0f, 360f), new Vector2(1500f, 110f));

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, 30f), new Vector2(1500f, 640f));

            contenedor = new GameObject("Avatares", typeof(RectTransform)).GetComponent<RectTransform>();
            contenedor.SetParent(raiz, false);
            ConstructorUI.Colocar(contenedor, new Vector2(0f, 170f), new Vector2(1400f, 560f));

            campoRuta = ConstructorCampos.CrearCampo(raiz, "Ruta", "Ruta de tu imagen (PNG o JPG), por ejemplo C:\\Fotos\\yo.png",
                new Vector2(-250f, -340f), new Vector2(1000f, 80f));
            botonSubir = ConstructorUI.CrearBoton(raiz, "Subir imagen", new Vector2(560f, -340f), new Vector2(380f, 80f),
                ConstructorUI.Azul, () => SubirImagenPulsado?.Invoke(campoRuta.text));

            botonContinuar = ConstructorUI.CrearBoton(raiz, "Volver", new Vector2(-290f, -440f), new Vector2(520f, 90f),
                ConstructorUI.Verde, () => ContinuarPulsado?.Invoke());
            ConstructorUI.CrearBoton(raiz, "Cerrar sesión", new Vector2(290f, -440f), new Vector2(520f, 90f),
                ConstructorUI.Gris, () => CerrarSesionPulsado?.Invoke());

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 34, FontStyles.Bold, Fallo,
                new Vector2(0f, -510f), new Vector2(1600f, 50f));
        }
    }
}