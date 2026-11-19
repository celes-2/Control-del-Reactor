using System;

namespace Reactor.Cliente.Presentadores
{
    public enum ModoAcceso
    {
        Login,
        Registro,
        Verificar,
        Olvide,
        Restablecer,
        GitHub,        // esperando que el jugador autorice el código en GitHub
    }

    /// <summary>Lo que el presentador puede pedirle a la escena de acceso (patrón MVP).</summary>
    public interface IAccesoVista
    {
        /// <summary>usuario o correo, contraseña.</summary>
        event Action<string, string> LoginPulsado;

        /// <summary>usuario, correo, contraseña, confirmación.</summary>
        event Action<string, string, string, string> RegistroPulsado;

        event Action<string> VerificarPulsado;
        event Action<string> OlvidePulsado;

        /// <summary>código, contraseña nueva, confirmación.</summary>
        event Action<string, string, string> RestablecerPulsado;

        /// <summary>El jugador pulsó un enlace para cambiar de pantalla.</summary>
        event Action<ModoAcceso> ModoPulsado;

        /// <summary>El jugador pulsó "Entrar con GitHub".</summary>
        event Action GitHubPulsado;

        /// <summary>El jugador canceló la espera del código de GitHub.</summary>
        event Action GitHubCancelado;

        /// <summary>Muestra el código que se escribe en la página de GitHub.</summary>
        void MostrarCodigoGitHub(string codigo, string direccion);

        void MostrarModo(ModoAcceso modo);
        void MostrarAviso(string texto, bool exito);
        void Bloquear(bool bloqueado);
        void LimpiarClaves();
        void Prellenar(string usuarioOCorreo);
    }
}
