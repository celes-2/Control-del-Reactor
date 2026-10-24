using System;
using System.Collections.Generic;
using Reactor.Cliente.Servicios;
using UnityEngine;

namespace Reactor.Cliente.Presentadores
{
    public enum SeccionInicio
    {
        Historial,
        Ranking
    }

    /// <summary>Lo que el presentador necesita de la pantalla de Inicio (US-04).</summary>
    public interface IInicioVista
    {
        event Action ElegirAvatarPulsado;
        event Action ConfiguracionPulsado;
        event Action CerrarSesionPulsado;

        void MostrarCargando(bool cargando);

        /// <param name="avatar">Imagen propia del jugador ya descargada, o null (los predeterminados los resuelve la vista).</param>
        void MostrarPerfil(PerfilJugador perfil, Sprite avatar);

        void MostrarHistorial(IReadOnlyList<PartidaHistorial> partidas);

        void MostrarRanking(IReadOnlyList<EntradaRankingJugador> porVictorias, IReadOnlyList<EntradaRankingJugador> porPuntos, string usuarioId);

        void MostrarErrorSeccion(SeccionInicio seccion, string mensaje);

        void MostrarAviso(string texto, bool exito);
    }
}
