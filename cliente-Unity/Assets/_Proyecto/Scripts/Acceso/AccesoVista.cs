using System;
using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Acceso
{
    /// <summary>
    /// Vista de la escena Acceso (MVP): login, registro, verificación del correo y recuperación de contraseña.
    /// Construye toda su interfaz por código: solo hace falta agregar este componente (y AccesoInstalador)
    /// a un objeto vacío de la escena. Cada modo es un grupo que se muestra u oculta.
    /// </summary>
    public sealed class AccesoVista : MonoBehaviour, IAccesoVista
    {
        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse. 0 = no se borra.")] float segundosAviso = 8f;

        static readonly Color Exito = new Color(0.45f, 0.9f, 0.6f);
        static readonly Color Fallo = new Color(1f, 0.55f, 0.45f);

        public event Action<string, string> LoginPulsado;
        public event Action<string, string, string, string> RegistroPulsado;
        public event Action<string> VerificarPulsado;
        public event Action<string> OlvidePulsado;
        public event Action<string, string, string> RestablecerPulsado;
        public event Action<ModoAcceso> ModoPulsado;

        readonly Dictionary<ModoAcceso, GameObject> grupos = new Dictionary<ModoAcceso, GameObject>();
        readonly List<Button> botones = new List<Button>();
        readonly List<TMP_InputField> camposClave = new List<TMP_InputField>();

        RectTransform raiz;
        TextMeshProUGUI titulo, aviso;
        Coroutine borrarAviso;

        TMP_InputField loginUsuario, loginClave;
        TMP_InputField regUsuario, regCorreo, regClave, regConfirmar;
        TMP_InputField verCodigo;
        TMP_InputField olvCorreo;
        TMP_InputField resCodigo, resClave, resConfirmar;

        void Awake() => ConstruirInterfaz();

        public void MostrarModo(ModoAcceso modo)
        {
            foreach (var par in grupos) par.Value.SetActive(par.Key == modo);
            titulo.text = modo switch
            {
                ModoAcceso.Login => "INICIAR SESIÓN",
                ModoAcceso.Registro => "CREAR CUENTA",
                ModoAcceso.Verificar => "VERIFICAR CORREO",
                ModoAcceso.Olvide => "RECUPERAR CONTRASEÑA",
                _ => "NUEVA CONTRASEÑA",
            };
        }

        public void MostrarAviso(string texto, bool exito)
        {
            aviso.color = exito ? Exito : Fallo;
            aviso.text = texto;
            if (borrarAviso != null) StopCoroutine(borrarAviso);
            if (!string.IsNullOrEmpty(texto) && segundosAviso > 0f) borrarAviso = StartCoroutine(BorrarAvisoLuego());
        }

        public void Bloquear(bool bloqueado)
        {
            foreach (var boton in botones) boton.interactable = !bloqueado;
        }

        public void LimpiarClaves()
        {
            foreach (var campo in camposClave) campo.text = "";
        }

        public void Prellenar(string usuarioOCorreo)
        {
            if (!string.IsNullOrEmpty(usuarioOCorreo)) loginUsuario.text = usuarioOCorreo;
        }

        IEnumerator BorrarAvisoLuego()
        {
            yield return new WaitForSeconds(segundosAviso);
            aviso.text = "";
            borrarAviso = null;
        }

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "AccesoUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            titulo = ConstructorUI.CrearTexto(raiz, "Titulo", 90, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 430f), new Vector2(1600f, 120f));

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, -10f), new Vector2(1100f, 760f));

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 36, FontStyles.Bold, Fallo,
                new Vector2(0f, -440f), new Vector2(1500f, 110f));

            ConstruirLogin();
            ConstruirRegistro();
            ConstruirVerificar();
            ConstruirOlvide();
            ConstruirRestablecer();
        }

        RectTransform Grupo(ModoAcceso modo)
        {
            var objeto = new GameObject("Modo " + modo, typeof(RectTransform));
            var rt = objeto.GetComponent<RectTransform>();
            rt.SetParent(raiz, false);
            ConstructorUI.Estirar(rt);
            grupos[modo] = objeto;
            return rt;
        }

        TMP_InputField Campo(RectTransform grupo, string nombre, string indicacion, float y, bool clave = false)
        {
            var campo = ConstructorCampos.CrearCampo(grupo, nombre, indicacion, new Vector2(0f, y), new Vector2(900f, 90f), clave);
            if (clave) camposClave.Add(campo);
            return campo;
        }

        Button Accion(RectTransform grupo, string texto, float y, Color color, Action alPulsar)
        {
            var boton = ConstructorUI.CrearBoton(grupo, texto, new Vector2(0f, y), new Vector2(520f, 100f), color, () => alPulsar());
            botones.Add(boton);
            return boton;
        }

        void Enlace(RectTransform grupo, string texto, float x, float y, ModoAcceso destino) =>
            ConstructorCampos.CrearEnlace(grupo, texto, new Vector2(x, y), new Vector2(520f, 60f), () => ModoPulsado?.Invoke(destino));

        void ConstruirLogin()
        {
            var g = Grupo(ModoAcceso.Login);
            loginUsuario = Campo(g, "Usuario", "Usuario o correo", 200f);
            loginClave = Campo(g, "Clave", "Contraseña", 90f, true);
            Accion(g, "Entrar", -50f, ConstructorUI.Verde, () => LoginPulsado?.Invoke(loginUsuario.text, loginClave.text));
            Enlace(g, "Crear cuenta", -250f, -190f, ModoAcceso.Registro);
            Enlace(g, "Olvidé mi contraseña", 250f, -190f, ModoAcceso.Olvide);
        }

        void ConstruirRegistro()
        {
            var g = Grupo(ModoAcceso.Registro);
            regUsuario = Campo(g, "Usuario", "Nombre de usuario (3 a 30 caracteres)", 270f);
            regCorreo = Campo(g, "Correo", "Correo electrónico", 160f);
            regClave = Campo(g, "Clave", "Contraseña", 50f, true);
            regConfirmar = Campo(g, "Confirmar", "Confirmar contraseña", -60f, true);
            ConstructorUI.CrearTexto(g, "Ayuda", 28, FontStyles.Normal, ConstructorUI.Gris,
                new Vector2(0f, -135f), new Vector2(1000f, 50f)).text = "Mínimo 8 caracteres con mayúscula, minúscula, número y símbolo.";
            Accion(g, "Registrarme", -230f, ConstructorUI.Verde,
                () => RegistroPulsado?.Invoke(regUsuario.text, regCorreo.text, regClave.text, regConfirmar.text));
            Enlace(g, "Ya tengo cuenta", 0f, -320f, ModoAcceso.Login);
        }

        void ConstruirVerificar()
        {
            var g = Grupo(ModoAcceso.Verificar);
            ConstructorUI.CrearTexto(g, "Info", 36, FontStyles.Normal, Color.white,
                new Vector2(0f, 230f), new Vector2(1000f, 120f)).text = "Escribe el código que te enviamos por correo.";
            verCodigo = Campo(g, "Codigo", "Código de verificación", 100f);
            Accion(g, "Verificar", -50f, ConstructorUI.Verde, () => VerificarPulsado?.Invoke(verCodigo.text));
            Enlace(g, "Volver al inicio de sesión", 0f, -190f, ModoAcceso.Login);
        }

        void ConstruirOlvide()
        {
            var g = Grupo(ModoAcceso.Olvide);
            ConstructorUI.CrearTexto(g, "Info", 36, FontStyles.Normal, Color.white,
                new Vector2(0f, 230f), new Vector2(1000f, 120f)).text = "Te enviaremos un código para cambiar tu contraseña.";
            olvCorreo = Campo(g, "Correo", "Correo electrónico", 100f);
            Accion(g, "Enviar código", -50f, ConstructorUI.Azul, () => OlvidePulsado?.Invoke(olvCorreo.text));
            Enlace(g, "Ya tengo un código", -250f, -190f, ModoAcceso.Restablecer);
            Enlace(g, "Volver", 250f, -190f, ModoAcceso.Login);
        }

        void ConstruirRestablecer()
        {
            var g = Grupo(ModoAcceso.Restablecer);
            resCodigo = Campo(g, "Codigo", "Código recibido", 200f);
            resClave = Campo(g, "Clave", "Contraseña nueva", 90f, true);
            resConfirmar = Campo(g, "Confirmar", "Confirmar contraseña nueva", -20f, true);
            Accion(g, "Cambiar contraseña", -150f, ConstructorUI.Azul,
                () => RestablecerPulsado?.Invoke(resCodigo.text, resClave.text, resConfirmar.text));
            Enlace(g, "Volver", 0f, -260f, ModoAcceso.Login);
        }
    }
}
