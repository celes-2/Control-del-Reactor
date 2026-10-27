using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Error de autenticación con el código estable del servidor (por ejemplo CORREO_NO_VERIFICADO).</summary>
    public sealed class ApiAuthException : Exception
    {
        public string Codigo { get; }

        public ApiAuthException(string codigo, string mensaje) : base(mensaje) => Codigo = codigo ?? "";
    }

    public sealed class SesionIniciada
    {
        public string Token;
        public string UsuarioId;
        public string Nombre;
        public string AvatarId;
    }

    public sealed class AvatarInfo
    {
        public string Id;
        public string Nombre;
        public string Color;
        /// <summary>true si es una imagen que cargó el propio jugador (se descarga del servidor).</summary>
        public bool Propio;
    }

    public sealed class PerfilUsuario
    {
        public string Id;
        public string Nombre;
        public string Correo;
        public string AvatarId;
    }

    /// <summary>Lo que Unity necesita del API REST de cuentas (/api/auth y /api/avatares).</summary>
    public interface IApiAuth
    {
        Task RegistrarAsync(string usuario, string correo, string clave, string confirmar);
        Task VerificarCorreoAsync(string codigo);
        Task<SesionIniciada> LoginAsync(string usuarioOCorreo, string clave);
        Task SolicitarRecuperacionAsync(string correo);
        Task RestablecerAsync(string codigo, string clave, string confirmar);

        // Requieren sesión iniciada (envían el token).
        Task<IReadOnlyList<AvatarInfo>> ListarAvataresAsync();
        Task<PerfilUsuario> PerfilAsync();
        Task ElegirAvatarAsync(string avatarId);

        /// <summary>Sube una imagen PNG (CA-06.3) y la deja como avatar. Devuelve el id del avatar nuevo.</summary>
        Task<string> SubirAvatarAsync(byte[] png);
    }
}
