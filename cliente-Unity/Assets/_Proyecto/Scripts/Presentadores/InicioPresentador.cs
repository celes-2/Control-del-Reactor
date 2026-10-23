using System;
using System.Threading.Tasks;
using Reactor.Cliente.Servicios;
using UnityEngine;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de la pantalla de Inicio (US-04): carga el perfil, el historial y los dos rankings. Si una sección falla,
    /// solo esa sección muestra el error y el resto se ve igual. Si la sesión ya no vale, cierra sesión.
    /// </summary>
    public sealed class InicioPresentador : IDisposable
    {
        readonly IInicioVista vista;
        readonly IApiPerfil api;
        readonly IAvatarCache imagenes;
        readonly string usuarioId;
        readonly Action<string> alConocerAvatar;
        readonly Action alElegirAvatar;
        readonly Action alConfiguracion;
        readonly Action alCerrarSesion;
        bool liberado;

        public InicioPresentador(
            IInicioVista vista, IApiPerfil api, IAvatarCache imagenes, string usuarioId,
            Action<string> alConocerAvatar, Action alElegirAvatar, Action alConfiguracion, Action alCerrarSesion)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.imagenes = imagenes ?? throw new ArgumentNullException(nameof(imagenes));
            this.usuarioId = usuarioId;
            this.alConocerAvatar = alConocerAvatar ?? throw new ArgumentNullException(nameof(alConocerAvatar));
            this.alElegirAvatar = alElegirAvatar ?? throw new ArgumentNullException(nameof(alElegirAvatar));
            this.alConfiguracion = alConfiguracion ?? throw new ArgumentNullException(nameof(alConfiguracion));
            this.alCerrarSesion = alCerrarSesion ?? throw new ArgumentNullException(nameof(alCerrarSesion));

            vista.ElegirAvatarPulsado += AlElegirAvatar;
            vista.ConfiguracionPulsado += AlConfiguracion;
            vista.CerrarSesionPulsado += AlCerrarSesion;
        }

        public async void Iniciar()
        {
            vista.MostrarCargando(true);

            // Las cuatro consultas salen a la vez; luego se muestran en orden.
            var perfil = api.ObtenerPerfilAsync();
            var historial = api.ObtenerHistorialAsync();
            var porVictorias = api.ObtenerRankingAsync("victorias");
            var porPuntos = api.ObtenerRankingAsync("puntos");

            if (!await CargarPerfilAsync(perfil)) return;
            await CargarHistorialAsync(historial);
            await CargarRankingAsync(porVictorias, porPuntos);

            if (!liberado) vista.MostrarCargando(false);
        }

        public void Dispose()
        {
            liberado = true;
            vista.ElegirAvatarPulsado -= AlElegirAvatar;
            vista.ConfiguracionPulsado -= AlConfiguracion;
            vista.CerrarSesionPulsado -= AlCerrarSesion;
        }

        /// <returns>false si hay que dejar de cargar (la sesión no vale o la pantalla se cerró).</returns>
        async Task<bool> CargarPerfilAsync(Task<PerfilJugador> tarea)
        {
            try
            {
                var perfil = await tarea;
                if (liberado) return false;

                Sprite imagen = null;
                if (!string.IsNullOrEmpty(perfil.AvatarId))
                {
                    try { imagen = await imagenes.ObtenerAsync(perfil.AvatarId); }
                    catch (Exception) { /* sin imagen propia se usa la predeterminada o el círculo de color */ }
                }
                if (liberado) return false;

                alConocerAvatar(perfil.AvatarId);
                vista.MostrarPerfil(perfil, imagen);
                return true;
            }
            catch (ApiAuthException e) when (e.Codigo == "NO_AUTORIZADO")
            {
                if (!liberado) alCerrarSesion();
                return false;
            }
            catch (Exception e)
            {
                if (!liberado) vista.MostrarAviso("No se pudo cargar tu perfil: " + e.Message, false);
                return !liberado;
            }
        }

        async Task CargarHistorialAsync(Task<System.Collections.Generic.IReadOnlyList<PartidaHistorial>> tarea)
        {
            try
            {
                var partidas = await tarea;
                if (!liberado) vista.MostrarHistorial(partidas);
            }
            catch (Exception e)
            {
                if (!liberado) vista.MostrarErrorSeccion(SeccionInicio.Historial, e.Message);
            }
        }

        async Task CargarRankingAsync(
            Task<System.Collections.Generic.IReadOnlyList<EntradaRankingJugador>> porVictorias,
            Task<System.Collections.Generic.IReadOnlyList<EntradaRankingJugador>> porPuntos)
        {
            try
            {
                var victorias = await porVictorias;
                var puntos = await porPuntos;
                if (!liberado) vista.MostrarRanking(victorias, puntos, usuarioId);
            }
            catch (Exception e)
            {
                if (!liberado) vista.MostrarErrorSeccion(SeccionInicio.Ranking, e.Message);
            }
        }

        void AlElegirAvatar() => alElegirAvatar();
        void AlConfiguracion() => alConfiguracion();
        void AlCerrarSesion() => alCerrarSesion();
    }
}
