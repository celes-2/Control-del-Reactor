using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Presentadores;
using UnityEngine;
using Reactor.Cliente.Ui;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Vista de la escena Arena (MVP). Recibe órdenes del ArenaPresentador y las
    /// reparte entre el mapa (ArenaRenderer), los avatares y el HUD.
    /// </summary>
    [RequireComponent(typeof(ArenaRenderer))]
    public sealed class ArenaVista : MonoBehaviour, IArenaVista
    {
        [Header("Opcional: imágenes de los avatares")]
        [Tooltip("Se asignan en orden de llegada (jugador 1, jugador 2...). Vacío = círculos con la inicial. " +
                 "Cuando exista el API, la imagen vendrá del avatar que eligió cada usuario.")]
        [SerializeField] Sprite[] spritesAvatar = new Sprite[0];

        [Tooltip("Tamaño del avatar completo. 1 = casi una celda; más de 1 invade las celdas vecinas.")]
        [SerializeField, Range(0.5f, 2.5f)] float tamanoAvatar = 1f;

        [Tooltip("Tamaño de la imagen respecto al anillo de color. Más de 1 = la imagen sobresale del anillo.")]
        [SerializeField, Range(0.3f, 2f)] float proporcionImagen = 0.76f;

        readonly Dictionary<string, AvatarVista> avatares = new Dictionary<string, AvatarVista>();
        ArenaRenderer mapa;
        ArenaHud hud;
        Transform contenedorAvatares;

        void Awake()
        {
            mapa = GetComponent<ArenaRenderer>();
            hud = GetComponent<ArenaHud>();
            if (hud == null) hud = gameObject.AddComponent<ArenaHud>();
            contenedorAvatares = new GameObject("Avatares").transform;
            contenedorAvatares.SetParent(transform, false);
        }

        public void DibujarEscenario(EscenarioMsg escenario)
        {
            if (!mapa.Dibujar(escenario)) hud.MostrarAviso("No se pudo dibujar la arena.");
        }

        public void LimpiarJugadores()
        {
            foreach (var avatar in avatares.Values)
                if (avatar != null) Destroy(avatar.gameObject);
            avatares.Clear();
        }

public void MostrarJugador(JugadorMsg jugador)
        {
            Debug.Log($"Arena: {jugador.Nombre} avatarId='{jugador.AvatarId}'");
            var posicion = mapa.CentroDeCelda(jugador.X, jugador.Y);
            if (avatares.TryGetValue(jugador.Id, out var existente))
            {
                existente.Colocar(posicion);
                return;
            }
            
            var nuevo = AvatarVista.Crear(contenedorAvatares, jugador, posicion, tamanoAvatar, proporcionImagen);
            
            // Nueva lógica de asignación de imagen
            var img = AvatarImagenes.DePredeterminado(jugador.AvatarId);
            if (img == null && spritesAvatar != null && spritesAvatar.Length > 0)
                img = spritesAvatar[avatares.Count % spritesAvatar.Length];
            if (img != null)
                nuevo.PonerImagen(img);

            avatares[jugador.Id] = nuevo;
        }

        public void MoverJugador(string jugadorId, int x, int y)
        {
            if (avatares.TryGetValue(jugadorId, out var avatar)) avatar.MoverA(mapa.CentroDeCelda(x, y));
        }

        public void MostrarAccion(string jugadorId, string mensaje)
        {
            if (avatares.TryGetValue(jugadorId, out var avatar)) avatar.MostrarMensaje(mensaje);
        }

        public void ActualizarMarcador(IEnumerable<JugadorMsg> jugadores)
        {
            var texto = new StringBuilder();
            foreach (var j in jugadores.OrderByDescending(x => x.Puntaje))
                texto.AppendLine($"<color={Colores.HexSeguro(j.Color)}>{j.Nombre}</color>   {j.Puntaje} pts");
            hud.MostrarMarcador(texto.ToString());
        }

        public void ActualizarTemporizador(int segundos, bool esCuentaRegresiva)
        {
            segundos = Mathf.Max(0, segundos);
            var etiqueta = esCuentaRegresiva ? "restante" : "transcurrido";
            hud.MostrarTemporizador($"{segundos / 60:00}:{segundos % 60:00}\n<size=40%>{etiqueta}</size>");
        }

        public void MostrarEstado(string texto) => hud.MostrarEstado(texto);

        public void MostrarAviso(string texto) => hud.MostrarAviso(texto);
    }
}
