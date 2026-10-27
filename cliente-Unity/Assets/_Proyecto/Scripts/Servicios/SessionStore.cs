using System;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Datos de la sesión del jugador. Vive durante toda la ejecución (lo crea el
    /// CompositionRoot), por eso la sesión se mantiene al navegar entre pantallas (CA-03.6).
    /// </summary>
    public sealed class SessionStore
    {
        public string Token { get; private set; }
        public string UsuarioId { get; private set; }
        public string NombreUsuario { get; private set; }

        /// <summary>Avatar elegido (id del catálogo o de la imagen propia). Null si todavía no eligió.</summary>
        public string AvatarId { get; private set; }

        public string PartidaActualId { get; set; }

        /// <summary>Resultado de la última partida terminada; lo lee la escena Victoria.</summary>
        public ResultadoPartida UltimoResultado { get; set; }

        public bool EstaAutenticado => !string.IsNullOrEmpty(Token);

        /// <summary>Sube cada vez que cambia quién es el jugador o su avatar; la barra de usuario lo observa.</summary>
        public int Version { get; private set; }

        public void Iniciar(string token, string usuarioId, string nombreUsuario)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("El token es obligatorio.", nameof(token));
            if (string.IsNullOrWhiteSpace(usuarioId)) throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

            Token = token;
            UsuarioId = usuarioId;
            NombreUsuario = nombreUsuario;
            AvatarId = null;
            Version++;
        }

        /// <summary>
        /// SOLO desarrollo: identifica a este equipo sin login (sin token). El WebSocket usa entonces ?userId=.
        /// Se reemplaza por Iniciar() cuando exista el login con JWT.
        /// </summary>
        public void IniciarProvisional(string usuarioId, string nombreUsuario)
        {
            if (string.IsNullOrWhiteSpace(usuarioId)) throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

            Token = null;
            UsuarioId = usuarioId;
            NombreUsuario = nombreUsuario;
            AvatarId = null;
            Version++;
        }

        public void CambiarAvatar(string avatarId)
        {
            AvatarId = string.IsNullOrEmpty(avatarId) ? null : avatarId;
            Version++;
        }

        public void Cerrar()
        {
            Token = null;
            UsuarioId = null;
            NombreUsuario = null;
            AvatarId = null;
            PartidaActualId = null;
            UltimoResultado = null;
            Version++;
        }
    }
}
