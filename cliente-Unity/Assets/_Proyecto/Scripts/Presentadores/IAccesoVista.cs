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

        void MostrarModo(ModoAcceso modo);
        void MostrarAviso(string texto, bool exito);
        void Bloquear(bool bloqueado);
        void LimpiarClaves();
        void Prellenar(string usuarioOCorreo);
    }
}
