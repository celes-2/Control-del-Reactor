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
        public string PartidaActualId { get; set; }

        public bool EstaAutenticado => !string.IsNullOrEmpty(Token);

        public void Iniciar(string token, string usuarioId, string nombreUsuario)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("El token es obligatorio.", nameof(token));
            if (string.IsNullOrWhiteSpace(usuarioId)) throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

            Token = token;
            UsuarioId = usuarioId;
            NombreUsuario = nombreUsuario;
        }

        public void Cerrar()
        {
            Token = null;
            UsuarioId = null;
            NombreUsuario = null;
            PartidaActualId = null;
        }
    }
}
