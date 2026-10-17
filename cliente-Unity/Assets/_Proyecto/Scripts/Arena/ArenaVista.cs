using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Presentadores;
using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Vista de la escena Arena (MVP). Recibe órdenes del ArenaPresentador y las
    /// reparte entre el mapa (ArenaRenderer), los avatares y el HUD.
    /// </summary>
    [RequireComponent(typeof(ArenaRenderer))]
    public sealed class ArenaVista : MonoBehaviour, IArenaVista
    {
        readonly Dictionary<string, AvatarVista> avatares = new Dictionary<string, AvatarVista>();
        ArenaRenderer mapa;
        ArenaHud hud;
        Transform contenedorAvatares;

        void Awake()
        {
            mapa = GetComponent<ArenaRenderer>();
            hud = gameObject.AddComponent<ArenaHud>();
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
            var posicion = mapa.CentroDeCelda(jugador.X, jugador.Y);
            if (avatares.TryGetValue(jugador.Id, out var existente))
            {
                existente.Colocar(posicion);
                return;
            }
            avatares[jugador.Id] = AvatarVista.Crear(contenedorAvatares, jugador, posicion);
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
