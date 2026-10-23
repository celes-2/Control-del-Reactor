using System;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de las pantallas de acceso: registro, verificación del correo, login y recuperación de contraseña
    /// (US-01, US-02, US-03, US-05). Es C# puro, así que se prueba con una vista y una API falsas.
    /// </summary>
    public sealed class AccesoPresentador : IDisposable
    {
        readonly IAccesoVista vista;
        readonly IApiAuth api;
        readonly Action<SesionIniciada> alIniciarSesion;

        bool ocupado;
        bool liberado;
        string usuarioPendiente;

        public AccesoPresentador(IAccesoVista vista, IApiAuth api, Action<SesionIniciada> alIniciarSesion)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.alIniciarSesion = alIniciarSesion ?? throw new ArgumentNullException(nameof(alIniciarSesion));

            vista.LoginPulsado += AlLogin;
            vista.RegistroPulsado += AlRegistro;
            vista.VerificarPulsado += AlVerificar;
            vista.OlvidePulsado += AlOlvide;
            vista.RestablecerPulsado += AlRestablecer;
            vista.ModoPulsado += AlCambiarModo;
        }

        public void Iniciar() => vista.MostrarModo(ModoAcceso.Login);

        public void Dispose()
        {
            liberado = true;
            vista.LoginPulsado -= AlLogin;
            vista.RegistroPulsado -= AlRegistro;
            vista.VerificarPulsado -= AlVerificar;
            vista.OlvidePulsado -= AlOlvide;
            vista.RestablecerPulsado -= AlRestablecer;
            vista.ModoPulsado -= AlCambiarModo;
        }

        void AlCambiarModo(ModoAcceso modo)
        {
            if (ocupado) return;
            vista.MostrarAviso("", true);
            vista.MostrarModo(modo);
        }

        async void AlLogin(string usuarioOCorreo, string clave)
        {
            if (Vacio(usuarioOCorreo) || Vacio(clave))
            {
                vista.MostrarAviso("Escribe tu usuario (o correo) y tu contraseña.", false);
                return;
            }

            await Ejecutar(async () =>
            {
                try
                {
                    var sesion = await api.LoginAsync(usuarioOCorreo.Trim(), clave);
                    if (liberado) return;
                    vista.LimpiarClaves();
                    alIniciarSesion(sesion);
                }
                catch (ApiAuthException e) when (e.Codigo == "CORREO_NO_VERIFICADO")
                {
                    if (liberado) return;
                    usuarioPendiente = usuarioOCorreo.Trim();
                    vista.MostrarModo(ModoAcceso.Verificar);
                    vista.MostrarAviso("Tu correo aún no está verificado. Escribe el código que te enviamos.", false);
                }
            });
        }

        async void AlRegistro(string usuario, string correo, string clave, string confirmar)
        {
            if (Vacio(usuario) || Vacio(correo) || Vacio(clave) || Vacio(confirmar))
            {
                vista.MostrarAviso("Completa todos los campos.", false);
                return;
            }

            await Ejecutar(async () =>
            {
                await api.RegistrarAsync(usuario.Trim(), correo.Trim(), clave, confirmar);
                if (liberado) return;
                usuarioPendiente = usuario.Trim();
                vista.LimpiarClaves();
                vista.MostrarModo(ModoAcceso.Verificar);
                vista.MostrarAviso("Cuenta creada. Te enviamos un código para verificar tu correo.", true);
            });
        }

        async void AlVerificar(string codigo)
        {
            if (Vacio(codigo))
            {
                vista.MostrarAviso("Escribe el código de verificación.", false);
                return;
            }

            await Ejecutar(async () =>
            {
                await api.VerificarCorreoAsync(codigo.Trim());
                if (liberado) return;
                vista.MostrarModo(ModoAcceso.Login);
                vista.Prellenar(usuarioPendiente);
                vista.MostrarAviso("Correo verificado. Ya puedes iniciar sesión.", true);
            });
        }

        async void AlOlvide(string correo)
        {
            if (Vacio(correo))
            {
                vista.MostrarAviso("Escribe el correo de tu cuenta.", false);
                return;
            }

            await Ejecutar(async () =>
            {
                await api.SolicitarRecuperacionAsync(correo.Trim());
                if (liberado) return;
                vista.MostrarModo(ModoAcceso.Restablecer);
                vista.MostrarAviso("Si la cuenta existe, te enviamos un código de recuperación.", true);
            });
        }

        async void AlRestablecer(string codigo, string clave, string confirmar)
        {
            if (Vacio(codigo) || Vacio(clave) || Vacio(confirmar))
            {
                vista.MostrarAviso("Completa todos los campos.", false);
                return;
            }

            await Ejecutar(async () =>
            {
                await api.RestablecerAsync(codigo.Trim(), clave, confirmar);
                if (liberado) return;
                vista.LimpiarClaves();
                vista.MostrarModo(ModoAcceso.Login);
                vista.MostrarAviso("Contraseña actualizada. Inicia sesión con la nueva.", true);
            });
        }

        /// <summary>Evita envíos dobles y convierte los errores del servidor en un aviso para el jugador.</summary>
        async System.Threading.Tasks.Task Ejecutar(Func<System.Threading.Tasks.Task> accion)
        {
            if (ocupado) return;
            ocupado = true;
            vista.Bloquear(true);
            vista.MostrarAviso("", true);
            try
            {
                await accion();
            }
            catch (ApiAuthException e)
            {
                if (!liberado) vista.MostrarAviso(e.Message, false);
            }
            catch (ApiException e)
            {
                if (!liberado) vista.MostrarAviso(e.Message, false);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                if (!liberado) vista.MostrarAviso("Ocurrió un error: " + e.Message, false);
            }
            finally
            {
                ocupado = false;
                if (!liberado) vista.Bloquear(false);
            }
        }

        static bool Vacio(string texto) => string.IsNullOrWhiteSpace(texto);
    }
}
