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

    /// <summary>Respuesta de POST /api/auth/github/iniciar (flujo de dispositivo de GitHub).</summary>
    public sealed class GitHubInicio
    {
        public string DeviceCode;
        /// <summary>Código que el jugador escribe en GitHub (por ejemplo ABCD-1234).</summary>
        public string UserCode;
        /// <summary>Página donde se escribe el código.</summary>
        public string VerificationUri;
        public int ExpiresIn;
        /// <summary>Segundos entre una consulta y la siguiente.</summary>
        public int Interval;
    }

    /// <summary>Respuesta de POST /api/auth/github/consultar: pendiente (Sesion = null) o completado.</summary>
    public sealed class GitHubEstado
    {
        public bool Completado;
        /// <summary>Si viene, la próxima consulta usa este intervalo (en segundos). 0 = el mismo.</summary>
        public int Interval;
        public SesionIniciada Sesion;
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

        /// <summary>Empieza el login con GitHub (US-29). Los fallos llegan como ApiAuthException con codigo LOGIN_SOCIAL_*.</summary>
        Task<GitHubInicio> IniciarGitHubAsync();

        /// <summary>Pregunta si el jugador ya autorizó el código en GitHub.</summary>
        Task<GitHubEstado> ConsultarGitHubAsync(string deviceCode);
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
