using System;
using System.Collections.Generic;
using System.Linq;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de la pantalla de perfil: muestra al jugador, deja elegir uno de los avatares del catálogo o subir
    /// una imagen propia desde el disco (US-04 / US-06). Hasta que haya un avatar elegido no se puede continuar
    /// (el servidor lo exige para entrar a una partida).
    /// </summary>
    public sealed class PerfilPresentador : IDisposable
    {
        readonly IPerfilVista vista;
        readonly IApiAuth api;
        readonly IPreparadorImagen preparador;
        readonly IAvatarCache imagenes;
        readonly Action<string> alCambiarAvatar;
        readonly Action alContinuar;
        readonly Action alCerrarSesion;

        IReadOnlyList<AvatarInfo> avatares = new List<AvatarInfo>();
        string seleccionado;
        bool ocupado;
        bool liberado;

        public PerfilPresentador(
            IPerfilVista vista, IApiAuth api, IPreparadorImagen preparador, IAvatarCache imagenes,
            Action<string> alCambiarAvatar, Action alContinuar, Action alCerrarSesion)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.preparador = preparador ?? throw new ArgumentNullException(nameof(preparador));
            this.imagenes = imagenes ?? throw new ArgumentNullException(nameof(imagenes));
            this.alCambiarAvatar = alCambiarAvatar ?? throw new ArgumentNullException(nameof(alCambiarAvatar));
            this.alContinuar = alContinuar ?? throw new ArgumentNullException(nameof(alContinuar));
            this.alCerrarSesion = alCerrarSesion ?? throw new ArgumentNullException(nameof(alCerrarSesion));

            vista.AvatarElegido += AlElegirAvatar;
            vista.SubirImagenPulsado += AlSubirImagen;
            vista.ContinuarPulsado += AlContinuar;
            vista.CerrarSesionPulsado += AlCerrarSesion;
        }

        public async void Iniciar()
        {
            vista.BloquearContinuar(true);
            try
            {
                var perfil = await api.PerfilAsync();
                await CargarAvataresAsync(perfil.AvatarId);
                if (liberado) return;

                vista.MostrarJugador(perfil.Nombre, perfil.Correo);
                alCambiarAvatar(seleccionado);
                if (avatares.Count == 0) vista.MostrarAviso("No hay avatares disponibles en el servidor.", false);
                else if (seleccionado == null) vista.MostrarAviso("Elige tu avatar para poder jugar.", true);
            }
            catch (Exception e)
            {
                MostrarError(e);
            }
        }

        public void Dispose()
        {
            liberado = true;
            vista.AvatarElegido -= AlElegirAvatar;
            vista.SubirImagenPulsado -= AlSubirImagen;
            vista.ContinuarPulsado -= AlContinuar;
            vista.CerrarSesionPulsado -= AlCerrarSesion;
        }

        async System.Threading.Tasks.Task CargarAvataresAsync(string elegidoEnElServidor)
        {
            var lista = await api.ListarAvataresAsync();
            if (liberado) return;

            avatares = lista ?? new List<AvatarInfo>();
            seleccionado = avatares.Any(a => a.Id == elegidoEnElServidor) ? elegidoEnElServidor : null;
            vista.MostrarAvatares(avatares, seleccionado);
            vista.BloquearContinuar(seleccionado == null);

            foreach (var propio in avatares.Where(a => a.Propio))
                CargarImagen(propio.Id);
        }

        async void CargarImagen(string avatarId)
        {
            try
            {
                var sprite = await imagenes.ObtenerAsync(avatarId);
                if (!liberado && sprite != null) vista.MostrarImagenAvatar(avatarId, sprite);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("No se pudo cargar la imagen del avatar: " + e.Message);
            }
        }

        async void AlElegirAvatar(string id)
        {
            if (ocupado || string.IsNullOrEmpty(id) || id == seleccionado) return;
            ocupado = true;
            try
            {
                await api.ElegirAvatarAsync(id);
                if (liberado) return;
                seleccionado = id;
                vista.MostrarAvatares(avatares, seleccionado);
                foreach (var propio in avatares.Where(a => a.Propio)) CargarImagen(propio.Id);
                vista.BloquearContinuar(false);
                alCambiarAvatar(id);
                vista.MostrarAviso("Avatar guardado.", true);
            }
            catch (Exception e)
            {
                MostrarError(e);
            }
            finally
            {
                ocupado = false;
            }
        }

        async void AlSubirImagen(string ruta)
        {
            if (ocupado) return;
            ocupado = true;
            vista.BloquearSubida(true);
            try
            {
                var png = preparador.Preparar(ruta);              // valida y reduce la imagen
                var avatarId = await api.SubirAvatarAsync(png);   // el servidor la guarda y la deja como avatar
                if (liberado) return;

                imagenes.Olvidar(avatarId);
                await CargarAvataresAsync(avatarId);
                if (liberado) return;

                vista.LimpiarRuta();
                alCambiarAvatar(seleccionado);
                vista.MostrarAviso("Imagen subida. Ya es tu avatar.", true);
            }
            catch (Exception e)
            {
                MostrarError(e);
            }
            finally
            {
                ocupado = false;
                if (!liberado) vista.BloquearSubida(false);
            }
        }

        void AlContinuar()
        {
            if (seleccionado == null)
            {
                vista.MostrarAviso("Elige un avatar para continuar.", false);
                return;
            }
            alContinuar();
        }

        void AlCerrarSesion() => alCerrarSesion();

        void MostrarError(Exception e)
        {
            if (liberado) return;
            if (!(e is ApiAuthException) && !(e is ApiException)) UnityEngine.Debug.LogException(e);
            vista.MostrarAviso(e.Message, false);
        }
    }
}
