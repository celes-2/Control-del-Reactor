using System;
using System.Linq;
using Reactor.Cliente.Servicios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Ui
{
    /// <summary>
    /// Franja con el avatar y el nombre del jugador, visible en TODAS las pantallas (CA-06.5).
    /// La crea el CompositionRoot (sobrevive a los cambios de escena) y se actualiza sola cuando cambia la sesión.
    /// Se oculta mientras no haya jugador (pantalla de Acceso).
    /// </summary>
    public sealed class BarraUsuario : MonoBehaviour
    {
        [SerializeField, Tooltip("Escenas donde la barra no se muestra (por ejemplo, Acceso).")]
        string[] ocultarEn = { "Acceso", "Arranque" };

        SessionStore sesion;
        IApiAuth auth;
        IAvatarCache imagenes;

        GameObject panel;
        Image circulo;
        TextMeshProUGUI inicial, nombre;
        int versionMostrada = -1;
        int generacion;
        string escenaMostrada;

        public void Iniciar(SessionStore sesion, IApiAuth auth, IAvatarCache imagenes)
        {
            this.sesion = sesion;
            this.auth = auth;
            this.imagenes = imagenes;
            Construir();
        }

        void Update()
        {
            if (sesion == null || panel == null) return;

            var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var hayJugador = !string.IsNullOrEmpty(sesion.UsuarioId) && !string.IsNullOrEmpty(sesion.NombreUsuario);
            var visible = hayJugador && !ocultarEn.Contains(escena);
            if (panel.activeSelf != visible) panel.SetActive(visible);

            if (!hayJugador || (versionMostrada == sesion.Version && escenaMostrada == escena)) return;
            versionMostrada = sesion.Version;
            escenaMostrada = escena;
            Refrescar();
        }

        async void Refrescar()
        {
            var miGeneracion = ++generacion;
            var avatarId = sesion.AvatarId;
            nombre.text = sesion.NombreUsuario;
            Poner(null, ConstructorUI.Gris, sesion.NombreUsuario);

            if (string.IsNullOrEmpty(avatarId)) return;

            try
            {
                var sprite = await imagenes.ObtenerAsync(avatarId);
                if (miGeneracion != generacion) return;      // llegó tarde: el avatar ya cambió otra vez
                if (sprite != null)
                {
                    Poner(sprite, Color.white, null);
                    return;
                }

                // Avatar predeterminado: solo tiene color. Se busca en el catálogo.
                if (!sesion.EstaAutenticado) return;
                var catalogo = await auth.ListarAvataresAsync();
                if (miGeneracion != generacion) return;
                var avatar = catalogo?.FirstOrDefault(a => a.Id == avatarId);
                if (avatar != null)
                {
                    var predet = AvatarImagenes.DePredeterminado(avatarId);
                    if (predet != null) Poner(predet, Color.white, null);
                    else Poner(null, ConstructorUI.ParseColor(avatar.Color, ConstructorUI.Gris), sesion.NombreUsuario);
                }
            }
            catch (Exception)
            {
                // Sin avatar visible no se rompe nada: se queda el círculo gris con la inicial.
            }
        }

        void Poner(Sprite sprite, Color color, string textoInicial)
        {
            circulo.sprite = sprite != null ? sprite : ConstructorUI.Circulo();
            circulo.color = color;
            circulo.preserveAspect = sprite != null;
            inicial.text = string.IsNullOrEmpty(textoInicial) ? "" : textoInicial.Substring(0, 1).ToUpperInvariant();
        }

        void Construir()
        {
            var lienzo = ConstructorUI.CrearLienzo(transform, "BarraUsuarioUI");
            lienzo.GetComponent<Canvas>().sortingOrder = 500;   // por encima de la interfaz de cada escena
            lienzo.GetComponent<GraphicRaycaster>().enabled = false;

            var fondo = ConstructorUI.CrearImagen(lienzo, "Barra", null, new Color(0.04f, 0.06f, 0.12f, 0.85f));
            var rt = fondo.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -90f);
            rt.sizeDelta = new Vector2(380f, 84f);
            fondo.raycastTarget = false;
            panel = fondo.gameObject;

            circulo = ConstructorUI.CrearImagen(rt, "Avatar", ConstructorUI.Circulo(), ConstructorUI.Gris);
            circulo.raycastTarget = false;
            ConstructorUI.Colocar(circulo.rectTransform, new Vector2(-135f, 0f), new Vector2(64f, 64f));
            inicial = ConstructorUI.CrearTexto(circulo.rectTransform, "Inicial", 36, FontStyles.Bold, Color.white,
                Vector2.zero, new Vector2(64f, 64f));

            nombre = ConstructorUI.CrearTexto(rt, "Nombre", 36, FontStyles.Bold, Color.white,
                new Vector2(40f, 0f), new Vector2(280f, 64f));
            nombre.alignment = TextAlignmentOptions.MidlineLeft;
            nombre.overflowMode = TextOverflowModes.Ellipsis;

            panel.SetActive(false);
        }
    }
}