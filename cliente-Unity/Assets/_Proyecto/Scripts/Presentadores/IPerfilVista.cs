using System;
using System.Collections.Generic;
using Reactor.Cliente.Servicios;
using UnityEngine;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>Lo que el presentador puede pedirle a la escena de perfil y avatar (patrón MVP, US-04 / US-06).</summary>
    public interface IPerfilVista
    {
        /// <summary>El jugador tocó un avatar (id).</summary>
        event Action<string> AvatarElegido;

        /// <summary>El jugador pidió subir la imagen de esa ruta (CA-06.3).</summary>
        event Action<string> SubirImagenPulsado;

        event Action ContinuarPulsado;
        event Action CerrarSesionPulsado;

        void MostrarJugador(string nombre, string correo);
        void MostrarAvatares(IReadOnlyList<AvatarInfo> avatares, string seleccionadoId);

        /// <summary>La imagen de un avatar propio ya se descargó: dibujarla en lugar del círculo de color.</summary>
        void MostrarImagenAvatar(string avatarId, Sprite sprite);

        void MostrarAviso(string texto, bool exito);
        void BloquearContinuar(bool bloqueado);
        void BloquearSubida(bool bloqueado);
        void LimpiarRuta();
    }
}
