using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Inicio
{
    /// <summary>
    /// Vista de la pantalla de Inicio (US-04): perfil del jugador (nombre, avatar, estado del correo, estadísticas) y
    /// tres pestañas: historial de partidas, ranking y logros. Construye su interfaz por código: solo hace falta agregar
    /// este componente (y InicioInstalador) a un objeto vacío de la escena.
    /// </summary>
    public sealed class InicioVista : MonoBehaviour, IInicioVista
    {
        enum Pestana
        {
            Historial,
            Ranking,
            Logros
        }

        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse. 0 = no se borra.")] float segundosAviso = 5f;

        static readonly Color Exito = new Color(0.45f, 0.9f, 0.6f);
        static readonly Color Fallo = new Color(1f, 0.55f, 0.45f);
        static readonly Color PestanaInactiva = new Color(0.2f, 0.25f, 0.36f, 1f);
        static readonly Color BarraFondo = new Color(0.2f, 0.24f, 0.34f, 1f);
        static readonly string[] EtiquetasEstadisticas =
            { "Partidas", "Victorias", "Derrotas", "Puntos totales", "Mejor puntaje", "Promedio" };

        public event Action ElegirAvatarPulsado;
        public event Action ConfiguracionPulsado;
        public event Action CerrarSesionPulsado;

        RectTransform raiz, contenido;
        TextMeshProUGUI nombre, correo, estadoCorreo, aviso, inicial;
        Image anillo, avatarImagen;
        readonly TextMeshProUGUI[] valores = new TextMeshProUGUI[6];
        readonly Image[] fondosPestana = new Image[3];
        Sprite circulo;
        Coroutine borrarAviso;

        // Datos que se dibujan según la pestaña elegida.
        Pestana pestana = Pestana.Historial;
        bool cargando = true;
        IReadOnlyList<PartidaHistorial> historial;
        IReadOnlyList<EntradaRankingJugador> rankingVictorias, rankingPuntos;
        IReadOnlyList<LogroJugador> logros;
        string usuarioId, errorHistorial, errorRanking;

        void Awake()
        {
            circulo = ConstructorUI.Circulo();
            ConstruirInterfaz();
            Redibujar();
        }

        // ---------- IInicioVista ----------

        public void MostrarCargando(bool valor)
        {
            cargando = valor;
            Redibujar();
        }

        public void MostrarPerfil(PerfilJugador perfil, Sprite avatar)
        {
            usuarioId = perfil.Id;
            nombre.text = perfil.Nombre;
            correo.text = perfil.Correo;
            estadoCorreo.text = perfil.CorreoVerificado ? "Correo verificado" : "Correo pendiente de verificación";
            estadoCorreo.color = perfil.CorreoVerificado ? Exito : Fallo;

            var imagen = avatar != null ? avatar : AvatarImagenes.DePredeterminado(perfil.AvatarId);
            if (imagen != null)
            {
                avatarImagen.sprite = imagen;
                avatarImagen.color = Color.white;
                avatarImagen.preserveAspect = true;
                inicial.text = "";
            }
            else
            {
                avatarImagen.sprite = circulo;
                avatarImagen.color = ConstructorUI.Gris;
                inicial.text = string.IsNullOrEmpty(perfil.Nombre) ? "?" : perfil.Nombre.Substring(0, 1).ToUpperInvariant();
            }

            var e = perfil.Estadisticas ?? new EstadisticasJugador();
            valores[0].text = e.PartidasJugadas.ToString(CultureInfo.InvariantCulture);
            valores[1].text = e.Victorias.ToString(CultureInfo.InvariantCulture);
            valores[2].text = e.Derrotas.ToString(CultureInfo.InvariantCulture);
            valores[3].text = e.PuntosTotales.ToString(CultureInfo.InvariantCulture);
            valores[4].text = e.MejorPuntaje.ToString(CultureInfo.InvariantCulture);
            valores[5].text = e.PromedioPuntos.ToString("0.0", CultureInfo.InvariantCulture);

            logros = perfil.Logros;
            Redibujar();
        }

        public void MostrarHistorial(IReadOnlyList<PartidaHistorial> partidas)
        {
            historial = partidas;
            errorHistorial = null;
            Redibujar();
        }

        public void MostrarRanking(IReadOnlyList<EntradaRankingJugador> porVictorias, IReadOnlyList<EntradaRankingJugador> porPuntos, string usuario)
        {
            rankingVictorias = porVictorias;
            rankingPuntos = porPuntos;
            if (string.IsNullOrEmpty(usuarioId)) usuarioId = usuario;
            errorRanking = null;
            Redibujar();
        }

        public void MostrarErrorSeccion(SeccionInicio seccion, string mensaje)
        {
            if (seccion == SeccionInicio.Historial) errorHistorial = mensaje;
            else errorRanking = mensaje;
            Redibujar();
        }

        public void MostrarAviso(string texto, bool exito)
        {
            aviso.color = exito ? Exito : Fallo;
            aviso.text = texto;
            if (borrarAviso != null) StopCoroutine(borrarAviso);
            if (!string.IsNullOrEmpty(texto) && segundosAviso > 0f) borrarAviso = StartCoroutine(BorrarAvisoLuego());
        }

        System.Collections.IEnumerator BorrarAvisoLuego()
        {
            yield return new WaitForSeconds(segundosAviso);
            aviso.text = "";
            borrarAviso = null;
        }

        // ---------- Contenido de las pestañas ----------

        void CambiarPestana(Pestana nueva)
        {
            pestana = nueva;
            Redibujar();
        }

        void Redibujar()
        {
            if (contenido == null) return;

            for (var i = 0; i < fondosPestana.Length; i++)
                fondosPestana[i].color = i == (int)pestana ? ConstructorUI.Azul : PestanaInactiva;

            for (var i = contenido.childCount - 1; i >= 0; i--)
                Destroy(contenido.GetChild(i).gameObject);

            if (cargando && DatosDe(pestana) == null)
            {
                Mensaje("Cargando...", ConstructorUI.Gris);
                return;
            }

            switch (pestana)
            {
                case Pestana.Historial: DibujarHistorial(); break;
                case Pestana.Ranking: DibujarRanking(); break;
                default: DibujarLogros(); break;
            }
        }

        object DatosDe(Pestana p) => p switch
        {
            Pestana.Historial => errorHistorial != null ? errorHistorial : (object)historial,
            Pestana.Ranking => errorRanking != null ? errorRanking : (object)rankingVictorias,
            _ => logros,
        };

        void DibujarHistorial()
        {
            if (errorHistorial != null) { Mensaje("No se pudo cargar el historial.\n" + errorHistorial, Fallo); return; }
            if (historial == null || historial.Count == 0) { Mensaje("Todavía no has jugado ninguna partida.", ConstructorUI.Gris); return; }

            Fila(290f, ConstructorUI.Gris, 26, FontStyles.Bold, "FECHA", "RESULTADO", "PUNTOS", "RIVALES", "DURACIÓN");
            var y = 235f;
            foreach (var partida in historial.Take(10))
            {
                var rivales = string.Join(", ", (partida.Jugadores ?? new List<JugadorHistorial>())
                    .Where(j => j.UsuarioId != usuarioId).Select(j => j.Nombre));
                var duracion = TimeSpan.FromSeconds(Math.Max(0, partida.DuracionSegundos));
                var resultado = partida.Resultado == "Victoria" ? "Victoria" : partida.Resultado == "Derrota" ? "Derrota" : "Sin ganador";
                var color = partida.Resultado == "Victoria" ? Exito : partida.Resultado == "Derrota" ? Fallo : Color.white;

                Fila(y, Color.white, 30, FontStyles.Normal,
                    partida.Fin.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
                    "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + resultado + "</color>",
                    partida.MiPuntaje + " pts",
                    string.IsNullOrEmpty(rivales) ? "-" : "vs " + rivales,
                    $"{(int)duracion.TotalMinutes}:{duracion.Seconds:00}");
                y -= 52f;
            }
        }

        static readonly float[] ColumnasHistorial = { -510f, -270f, -90f, 50f, 390f };
        static readonly float[] AnchosHistorial = { 230f, 170f, 130f, 330f, 120f };

        void Fila(float y, Color color, float tamano, FontStyles estilo, params string[] textos)
        {
            for (var i = 0; i < textos.Length; i++)
                Celda(textos[i], ColumnasHistorial[i], y, AnchosHistorial[i], color, tamano, estilo, TextAlignmentOptions.Left);
        }

        void DibujarRanking()
        {
            if (errorRanking != null) { Mensaje("No se pudo cargar el ranking.\n" + errorRanking, Fallo); return; }
            if ((rankingVictorias == null || rankingVictorias.Count == 0) && (rankingPuntos == null || rankingPuntos.Count == 0))
            {
                Mensaje("Todavía no hay partidas jugadas.", ConstructorUI.Gris);
                return;
            }

            Bloque(-510f, "POR VICTORIAS", rankingVictorias, j => j.Victorias + (j.Victorias == 1 ? " victoria" : " victorias"));
            Bloque(20f, "POR PUNTOS", rankingPuntos, j => j.PuntosTotales + " pts");
        }

        void Bloque(float izquierda, string titulo, IReadOnlyList<EntradaRankingJugador> lista, Func<EntradaRankingJugador, string> valor)
        {
            Celda(titulo, izquierda, 290f, 490f, ConstructorUI.Arena, 34, FontStyles.Bold, TextAlignmentOptions.Left);
            var y = 235f;
            foreach (var jugador in (lista ?? new List<EntradaRankingJugador>()).Take(8))
            {
                var esYo = !string.IsNullOrEmpty(usuarioId) && jugador.UsuarioId == usuarioId;
                var color = esYo ? ConstructorUI.Dorado : Color.white;
                Celda(jugador.Posicion + ".", izquierda, y, 60f, color, 30, FontStyles.Bold, TextAlignmentOptions.Left);
                Celda(jugador.Nombre + (esYo ? "  (tú)" : ""), izquierda + 60f, y, 270f, color, 30,
                    esYo ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Left);
                Celda(valor(jugador), izquierda + 330f, y, 160f, color, 28, FontStyles.Normal, TextAlignmentOptions.Right);
                y -= 52f;
            }
        }

        void DibujarLogros()
        {
            if (logros == null || logros.Count == 0) { Mensaje("No hay logros para mostrar.", ConstructorUI.Gris); return; }

            var y = 230f;
            foreach (var logro in logros)
            {
                var color = logro.Obtenido ? ConstructorUI.Dorado : Color.white;
                Celda(logro.Nombre, -510f, y + 22f, 600f, color, 38, FontStyles.Bold, TextAlignmentOptions.Left);
                Celda(logro.Descripcion, -510f, y - 22f, 620f, ConstructorUI.Gris, 27, FontStyles.Normal, TextAlignmentOptions.Left);

                // Barra de avance.
                var meta = Math.Max(1, logro.Meta);
                var fraccion = Mathf.Clamp01((float)logro.Progreso / meta);
                var fondo = ConstructorUI.CrearImagen(contenido, "BarraFondo", null, BarraFondo);
                ConstructorUI.Colocar(fondo.rectTransform, new Vector2(230f, y), new Vector2(300f, 20f));
                var relleno = ConstructorUI.CrearImagen(contenido, "BarraRelleno", null, logro.Obtenido ? ConstructorUI.Dorado : ConstructorUI.Azul);
                var rt = relleno.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(80f, y);
                rt.sizeDelta = new Vector2(300f * fraccion, 20f);

                Celda(logro.Obtenido ? "Obtenido" : Math.Min(logro.Progreso, meta) + " / " + meta, 400f, y, 120f,
                    logro.Obtenido ? Exito : ConstructorUI.Gris, 26, FontStyles.Bold, TextAlignmentOptions.Right);
                y -= 130f;
            }
        }

        void Mensaje(string texto, Color color) =>
            ConstructorUI.CrearTexto(contenido, "Mensaje", 36, FontStyles.Normal, color, Vector2.zero, new Vector2(1000f, 240f)).text = texto;

        /// <summary>Un texto dentro del contenido de la pestaña; "izquierda" es el borde izquierdo en coordenadas del contenido.</summary>
        void Celda(string texto, float izquierda, float y, float ancho, Color color, float tamano, FontStyles estilo, TextAlignmentOptions alineacion)
        {
            var t = ConstructorUI.CrearTexto(contenido, "Celda", tamano, estilo, color,
                new Vector2(izquierda + ancho / 2f, y), new Vector2(ancho, 50f));
            t.alignment = alineacion;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.text = texto;
        }

        // ---------- Construcción de la interfaz ----------

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "InicioUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            ConstructorUI.CrearTexto(raiz, "Titulo", 80, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 470f), new Vector2(900f, 100f)).text = "INICIO";

            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 34, FontStyles.Bold, Fallo,
                new Vector2(0f, 395f), new Vector2(1500f, 50f));

            // ----- Panel izquierdo: el jugador -----
            var panelJugador = ConstructorUI.CrearImagen(raiz, "PanelJugador", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panelJugador.rectTransform, new Vector2(-560f, -30f), new Vector2(640f, 760f));

            anillo = ConstructorUI.CrearImagen(raiz, "Anillo", circulo, ConstructorUI.Dorado);
            ConstructorUI.Colocar(anillo.rectTransform, new Vector2(-560f, 235f), new Vector2(210f, 210f));
            avatarImagen = ConstructorUI.CrearImagen(raiz, "Avatar", circulo, ConstructorUI.Gris);
            ConstructorUI.Colocar(avatarImagen.rectTransform, new Vector2(-560f, 235f), new Vector2(190f, 190f));
            inicial = ConstructorUI.CrearTexto(avatarImagen.rectTransform, "Inicial", 90, FontStyles.Bold, Color.white,
                Vector2.zero, new Vector2(190f, 190f));

            nombre = ConstructorUI.CrearTexto(raiz, "Nombre", 52, FontStyles.Bold, Color.white,
                new Vector2(-560f, 85f), new Vector2(600f, 70f));
            correo = ConstructorUI.CrearTexto(raiz, "Correo", 28, FontStyles.Normal, ConstructorUI.Gris,
                new Vector2(-560f, 40f), new Vector2(600f, 40f));
            estadoCorreo = ConstructorUI.CrearTexto(raiz, "EstadoCorreo", 30, FontStyles.Bold, ConstructorUI.Gris,
                new Vector2(-560f, 0f), new Vector2(600f, 40f));

            for (var i = 0; i < 6; i++)
            {
                var x = -560f + (i % 2 == 0 ? -150f : 150f);
                var y = -75f - (i / 2) * 120f;
                var tarjeta = ConstructorUI.CrearImagen(raiz, "Tarjeta " + EtiquetasEstadisticas[i], null, ConstructorUI.Fondo);
                ConstructorUI.Colocar(tarjeta.rectTransform, new Vector2(x, y), new Vector2(290f, 104f));
                ConstructorUI.CrearTexto(raiz, "Etiqueta " + EtiquetasEstadisticas[i], 24, FontStyles.Normal, ConstructorUI.Gris,
                    new Vector2(x, y + 28f), new Vector2(280f, 30f)).text = EtiquetasEstadisticas[i];
                valores[i] = ConstructorUI.CrearTexto(raiz, "Valor " + EtiquetasEstadisticas[i], 50, FontStyles.Bold, Color.white,
                    new Vector2(x, y - 14f), new Vector2(280f, 60f));
                valores[i].text = "-";
            }

            // ----- Panel derecho: pestañas -----
            var panelDatos = ConstructorUI.CrearImagen(raiz, "PanelDatos", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panelDatos.rectTransform, new Vector2(340f, -30f), new Vector2(1100f, 760f));

            var nombres = new[] { "Historial", "Ranking", "Logros" };
            for (var i = 0; i < nombres.Length; i++)
            {
                var indice = i;
                var boton = ConstructorUI.CrearBoton(raiz, nombres[i], new Vector2(10f + i * 330f, 305f), new Vector2(310f, 70f),
                    PestanaInactiva, () => CambiarPestana((Pestana)indice));
                fondosPestana[i] = (Image)boton.targetGraphic;
            }

            contenido = new GameObject("Contenido", typeof(RectTransform)).GetComponent<RectTransform>();
            contenido.SetParent(raiz, false);
            ConstructorUI.Colocar(contenido, new Vector2(340f, -75f), new Vector2(1040f, 640f));

            // ----- Botones de abajo -----
            ConstructorUI.CrearBoton(raiz, "Elegir avatar", new Vector2(-420f, -470f), new Vector2(380f, 90f),
                ConstructorUI.Azul, () => ElegirAvatarPulsado?.Invoke());
            ConstructorUI.CrearBoton(raiz, "Configuración", new Vector2(0f, -470f), new Vector2(380f, 90f),
                ConstructorUI.Verde, () => ConfiguracionPulsado?.Invoke());
            ConstructorUI.CrearBoton(raiz, "Cerrar sesión", new Vector2(420f, -470f), new Vector2(380f, 90f),
                ConstructorUI.Gris, () => CerrarSesionPulsado?.Invoke());
        }
    }
}
