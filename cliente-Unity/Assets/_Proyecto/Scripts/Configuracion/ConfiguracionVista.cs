using System;
using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reactor.Cliente.Configuracion
{
    /// <summary>
    /// Vista de la escena Configuración (MVP, US-08). Construye su interfaz por código: solo hace falta
    /// agregar este componente (y ConfiguracionInstalador) a un objeto vacío de la escena.
    /// Para cambiar textos, colores o posiciones, editar ConstruirInterfaz.
    /// </summary>
    public sealed class ConfiguracionVista : MonoBehaviour, IConfiguracionVista
    {
        [SerializeField, Tooltip("Segundos que se ve un aviso antes de borrarse.")] float segundosAviso = 4f;

        static readonly Color Apagado = ConstructorUI.Apagado;

        public event Action<CampoAjuste, int> AjusteCambiado;
        public event Action<ModoFin> ModoElegido;
        public event Action CrearPulsado;
        public event Action UnirsePulsado;
        public event Action<string> EscenarioElegido;
        public event Action VolverAEscenariosPulsado;

        RectTransform raiz;
        Image botonTiempo, botonPuntos;
        TextMeshProUGUI etiquetaValor, valor, congelamiento, jugadores, guardianes, aviso;
        Button botonCrear;
        Coroutine borrarAviso;
        RectTransform grupoConfig, grupoSelector, contenedorTarjetas;
        readonly List<Texture2D> texturasTarjetas = new List<Texture2D>();

        void Awake()
        {
            ConstruirInterfaz();
            MostrarPaso(true);
        }

        public void MostrarModo(ModoFin modo)
        {
            botonTiempo.color = modo == ModoFin.Tiempo ? ConstructorUI.Azul : Apagado;
            botonPuntos.color = modo == ModoFin.Puntos ? ConstructorUI.Azul : Apagado;
        }

        public void MostrarValor(string etiqueta, string texto)
        {
            etiquetaValor.text = etiqueta;
            valor.text = texto;
        }

        public void MostrarCongelamiento(string texto) => congelamiento.text = texto;

        public void MostrarJugadores(string texto) => jugadores.text = texto;

        public void MostrarGuardianes(string texto) => guardianes.text = texto;

        // El escenario se elige en su propia pantalla; aquí no se muestra.
        public void MostrarEscenario(EscenarioInfo info) { }

        const int PixelesPorCelda = 4;

        /// <summary>Dibuja el escenario real (paredes, trampas, energía, puertas, guardianes y adornos); null si no tiene dibujo.</summary>
        Texture2D CrearTextura(EscenarioInfo info, out int ancho, out int alto)
        {
            var filas = info.Filas;
            ancho = 0;
            alto = filas.Count;
            if (alto == 0) return null;
            foreach (var fila in filas) ancho = Mathf.Max(ancho, fila.Length);

            var textura = new Texture2D(ancho * PixelesPorCelda, alto * PixelesPorCelda, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            var tema = ColorTema(info.Tema);
            // Si el escenario trae imágenes propias, la vista previa usa su color medio.
            tema.Piso = EscenarioArchivo.ColorPromedio(info.TilePiso) ?? tema.Piso;
            tema.Muro = EscenarioArchivo.ColorPromedio(info.TilePared) ?? tema.Muro;
            for (var y = 0; y < alto; y++)
                for (var x = 0; x < ancho; x++)
                {
                    var c = x < filas[y].Length ? filas[y][x] : '#';
                    var color = c == '#' || c == 'W' ? tema.Muro : c == '.' ? tema.Piso : ColorCelda(c);
                    PintarCelda(textura, x, y, alto, ancho, color, false);
                }

            // Marcas (más pequeñas que la celda, para ver el piso debajo): adornos, energía, trampas, puertas, guardianes.
            foreach (var d in info.Decoracion) PintarCelda(textura, d.x, d.y, alto, ancho, tema.Adorno, true);
            foreach (var e in info.PuntosEnergia) PintarCelda(textura, e.x, e.y, alto, ancho, ColorEnergia, true);
            foreach (var tr in info.Trampas) PintarCelda(textura, tr.x, tr.y, alto, ancho, ConstructorUI.Rojo, true);
            foreach (var puerta in info.Puertas) PintarCelda(textura, puerta.x, puerta.y, alto, ancho, ColorPuerta, false);
            foreach (var guardian in info.InicioGuardianes) PintarCelda(textura, guardian.x, guardian.y, alto, ancho, ColorGuardian, true);
            textura.Apply();
            return textura;
        }

        static readonly Color ColorPuerta = new Color(0.85f, 0.55f, 0.20f);
        static readonly Color ColorGuardian = new Color(0.70f, 0.35f, 0.90f);
        static readonly Color ColorEnergia = new Color(1f, 0.85f, 0.25f);

        struct ColoresTema { public Color Piso, Muro, Adorno; }

        static ColoresTema ColorTema(string tema)
        {
            switch (tema)
            {
                case "investigacion": return new ColoresTema { Piso = new Color(0.20f, 0.27f, 0.30f), Muro = new Color(0.62f, 0.74f, 0.80f), Adorno = new Color(0.55f, 0.90f, 1f) };
                case "laboratorio": return new ColoresTema { Piso = new Color(0.13f, 0.22f, 0.17f), Muro = new Color(0.36f, 0.52f, 0.38f), Adorno = new Color(0.75f, 0.90f, 0.35f) };
                case "estacion": return new ColoresTema { Piso = new Color(0.10f, 0.13f, 0.24f), Muro = new Color(0.42f, 0.46f, 0.64f), Adorno = new Color(0.40f, 0.75f, 1f) };
                case "militar": return new ColoresTema { Piso = new Color(0.30f, 0.28f, 0.18f), Muro = new Color(0.46f, 0.50f, 0.32f), Adorno = new Color(0.78f, 0.72f, 0.45f) };
                default: return new ColoresTema { Piso = new Color(0.07f, 0.09f, 0.15f), Muro = new Color(0.30f, 0.34f, 0.44f), Adorno = new Color(0.6f, 0.6f, 0.7f) };
            }
        }

        /// <summary>Pinta una celda (4 x 4 píxeles). Con marca=true solo el centro de 2 x 2.</summary>
        static void PintarCelda(Texture2D textura, int x, int y, int alto, int ancho, Color color, bool marca)
        {
            if (x < 0 || y < 0 || x >= ancho || y >= alto) return;
            var desde = marca ? 1 : 0;
            var hasta = marca ? PixelesPorCelda - 1 : PixelesPorCelda;
            for (var py = desde; py < hasta; py++)
                for (var px = desde; px < hasta; px++)
                    textura.SetPixel(x * PixelesPorCelda + px, (alto - 1 - y) * PixelesPorCelda + py, color);
        }

        static Color ColorCelda(char c)
        {
            switch (c)
            {
                case '#':
                case 'W': return new Color(0.30f, 0.34f, 0.44f);
                case 'R': return ConstructorUI.Dorado;
                case 'T': return ConstructorUI.Rojo;
                case 'S':
                case 'A': return ConstructorUI.Azul;
                case 'B': return ConstructorUI.Verde;
                default: return new Color(0.07f, 0.09f, 0.15f);
            }
        }

        void OnDestroy()
        {
            LiberarTarjetas();
        }

        public void MostrarPaso(bool eligiendoEscenario)
        {
            grupoSelector.gameObject.SetActive(eligiendoEscenario);
            grupoConfig.gameObject.SetActive(!eligiendoEscenario);
        }

        void LiberarTarjetas()
        {
            foreach (var textura in texturasTarjetas)
                if (textura != null) Destroy(textura);
            texturasTarjetas.Clear();
        }

        public void MostrarSelectorEscenarios(IReadOnlyList<EscenarioInfo> escenarios, string idActual)
        {
            LiberarTarjetas();
            for (var i = contenedorTarjetas.childCount - 1; i >= 0; i--)
                Destroy(contenedorTarjetas.GetChild(i).gameObject);

            var cantidad = escenarios.Count;
            var anchoTarjeta = cantidad <= 4 ? 420f : 340f;
            var paso = anchoTarjeta + 20f;
            for (var i = 0; i < cantidad; i++)
            {
                var info = escenarios[i];
                var id = info.Id;
                var actual = id == (idActual ?? "");
                var fondo = actual ? ConstructorUI.AzulHielo : ConstructorUI.Panel;
                var texto = ConstructorUI.TextoSobre(fondo);
                var x = (i - (cantidad - 1) / 2f) * paso;

                var tarjeta = ConstructorUI.CrearBoton(contenedorTarjetas, info.Nombre, new Vector2(x, -35f),
                    new Vector2(anchoTarjeta, 570f), fondo, () => EscenarioElegido?.Invoke(id));
                var rt = (RectTransform)tarjeta.transform;
                var etiquetaBoton = tarjeta.GetComponentInChildren<TextMeshProUGUI>();
                etiquetaBoton.text = ""; // el nombre se escribe abajo, en su propio sitio

                // Vista previa del escenario real.
                var cajaAncho = anchoTarjeta - 40f;
                var textura = CrearTextura(info, out var celdasAncho, out var celdasAlto);
                if (textura != null)
                {
                    texturasTarjetas.Add(textura);
                    var imagen = new GameObject("Vista previa", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    imagen.transform.SetParent(rt, false);
                    imagen.texture = textura;
                    imagen.raycastTarget = false;
                    var escala = Mathf.Min(cajaAncho / celdasAncho, 260f / celdasAlto);
                    ConstructorUI.Colocar(imagen.rectTransform, new Vector2(0f, 115f), new Vector2(celdasAncho * escala, celdasAlto * escala));
                }
                else
                {
                    var sinDibujo = ConstructorUI.CrearTexto(rt, "Sin vista previa", 26, FontStyles.Italic, texto,
                        new Vector2(0f, 115f), new Vector2(cajaAncho, 260f));
                    sinDibujo.text = "Mapa del servidor\n(sin vista previa)";
                }

                ConstructorUI.CrearTexto(rt, "Nombre", 32, FontStyles.Bold, texto, new Vector2(0f, -75f), new Vector2(cajaAncho, 90f))
                    .text = info.Nombre;
                var descripcion = ConstructorUI.CrearTexto(rt, "Descripcion", 24, FontStyles.Normal, texto,
                    new Vector2(0f, -183f), new Vector2(cajaAncho, 110f));
                descripcion.alignment = TextAlignmentOptions.Top;
                // Las descripciones largas se encogen para no pisar la línea de datos.
                descripcion.enableAutoSizing = true;
                descripcion.fontSizeMax = 22f;
                descripcion.fontSizeMin = 14f;
                descripcion.text = info.Descripcion;

                // Solo se muestra lo que se conoce: el mapa clásico no informa trampas ni energías.
                var trampas = info.Trampas.Count > 0 ? info.Trampas.Count : info.TrampasAleatorias;
                var energias = info.PuntosEnergia.Count;
                if (trampas > 0 || energias > 0)
                    ConstructorUI.CrearTexto(rt, "Datos", 22, FontStyles.Normal, texto, new Vector2(0f, -262f), new Vector2(cajaAncho, 40f))
                        .text = $"{trampas} trampas  ·  {energias} puntos de energía";
            }
        }

        public void BloquearCrear(bool bloqueado) => ConstructorUI.Habilitar(botonCrear, !bloqueado);

        public void MostrarAviso(string texto)
        {
            aviso.text = texto;
            if (borrarAviso != null) StopCoroutine(borrarAviso);
            if (!string.IsNullOrEmpty(texto)) borrarAviso = StartCoroutine(BorrarAvisoLuego());
        }

        IEnumerator BorrarAvisoLuego()
        {
            yield return new WaitForSeconds(segundosAviso);
            aviso.text = "";
            borrarAviso = null;
        }

        void ConstruirInterfaz()
        {
            raiz = ConstructorUI.CrearLienzo(transform, "ConfiguracionUI");

            var fondo = ConstructorUI.CrearImagen(raiz, "Fondo", null, ConstructorUI.Fondo);
            ConstructorUI.Estirar(fondo.rectTransform);

            // Dos pasos en la misma escena: primero elegir el escenario, después los ajustes.
            var lienzo = raiz;
            grupoConfig = NuevoGrupo(lienzo, "Ajustes");
            grupoSelector = NuevoGrupo(lienzo, "Elegir escenario");
            ConstruirSelector(grupoSelector);
            raiz = grupoConfig;

            ConstructorUI.CrearTexto(raiz, "Titulo", 100, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 400f), new Vector2(1500f, 130f)).text = "CONFIGURACION";

            var panel = ConstructorUI.CrearImagen(raiz, "Panel", null, ConstructorUI.Panel);
            ConstructorUI.Colocar(panel.rectTransform, new Vector2(0f, 20f), new Vector2(1500f, 640f));

            // Fila 1: cómo termina la partida.
            Etiqueta("Fin de la partida", 270f);
            botonTiempo = ConstructorUI.CrearBoton(raiz, "Por tiempo", new Vector2(130f, 270f), new Vector2(320f, 90f),
                Apagado, () => ModoElegido?.Invoke(ModoFin.Tiempo)).targetGraphic as Image;
            botonPuntos = ConstructorUI.CrearBoton(raiz, "Por puntos", new Vector2(480f, 270f), new Vector2(320f, 90f),
                Apagado, () => ModoElegido?.Invoke(ModoFin.Puntos)).targetGraphic as Image;

            // Fila 2: duración o puntos, según el modo.
            etiquetaValor = Etiqueta("Duración", 170f);
            valor = Selector(170f, CampoAjuste.Valor);

            // Fila 3: congelamiento.
            Etiqueta("Congelamiento", 70f);
            congelamiento = Selector(70f, CampoAjuste.Congelamiento);

            // Fila 4: máximo de jugadores.
            Etiqueta("Máximo de jugadores", -30f);
            jugadores = Selector(-30f, CampoAjuste.Jugadores);

            // Fila 5: guardianes (US-21).
            Etiqueta("Guardianes", -130f);
            guardianes = Selector(-130f, CampoAjuste.Guardianes);


            // Tres botones en la misma fila: para cambiar de escenario se vuelve a su pantalla.
            var tamano = new Vector2(460f, 100f);
            var escenarios = ConstructorUI.CrearBoton(raiz, "Escenarios", new Vector2(-490f, -380f), tamano,
                ConstructorUI.Azul, () => VolverAEscenariosPulsado?.Invoke());
            botonCrear = ConstructorUI.CrearBoton(raiz, "Crear partida", new Vector2(0f, -380f), tamano,
                ConstructorUI.Verde, () => CrearPulsado?.Invoke());
            var unirse = ConstructorUI.CrearBoton(raiz, "Unirse a una partida", new Vector2(490f, -380f), tamano,
                ConstructorUI.Azul, () => UnirsePulsado?.Invoke());
            // Con tres botones el texto es más chico para que "Unirse a una partida" quepa en una línea.
            foreach (var boton in new[] { escenarios, botonCrear, unirse })
                boton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 36;

            raiz = lienzo; // el aviso se ve en los dos pasos
            aviso = ConstructorUI.CrearTexto(raiz, "Aviso", 40, FontStyles.Bold, new Color(1f, 0.55f, 0.45f),
                new Vector2(0f, -465f), new Vector2(1500f, 70f));
        }

        static RectTransform NuevoGrupo(RectTransform padre, string nombre)
        {
            var grupo = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
            grupo.SetParent(padre, false);
            ConstructorUI.Estirar(grupo);
            return grupo;
        }

        void ConstruirSelector(RectTransform grupo)
        {
            ConstructorUI.CrearTexto(grupo, "Titulo", 100, FontStyles.Bold, ConstructorUI.Arena,
                new Vector2(0f, 400f), new Vector2(1700f, 130f)).text = "ELIGE EL ESCENARIO";
            ConstructorUI.CrearTexto(grupo, "Indicacion", 36, FontStyles.Normal, ConstructorUI.Gris,
                new Vector2(0f, 315f), new Vector2(1500f, 60f)).text = "Toca uno para continuar con la configuración";

            contenedorTarjetas = new GameObject("Tarjetas", typeof(RectTransform)).GetComponent<RectTransform>();
            contenedorTarjetas.SetParent(grupo, false);
            ConstructorUI.Estirar(contenedorTarjetas);

            ConstructorUI.CrearBoton(grupo, "Unirse a una partida", new Vector2(0f, -380f), new Vector2(520f, 100f),
                ConstructorUI.Azul, () => UnirsePulsado?.Invoke());
        }

        TextMeshProUGUI Etiqueta(string texto, float y)
        {
            var etiqueta = ConstructorUI.CrearTexto(raiz, "Etiqueta " + texto, 44, FontStyles.Bold, Color.white,
                new Vector2(-380f, y), new Vector2(600f, 80f));
            etiqueta.alignment = TextAlignmentOptions.Left;
            etiqueta.text = texto;
            return etiqueta;
        }

        /// <summary>Botón -, texto del valor y botón +.</summary>
        TextMeshProUGUI Selector(float y, CampoAjuste campo)
        {
            ConstructorUI.CrearBoton(raiz, "-", new Vector2(20f, y), new Vector2(90f, 90f),
                ConstructorUI.Azul, () => AjusteCambiado?.Invoke(campo, -1));
            var texto = ConstructorUI.CrearTexto(raiz, "Valor " + campo, 48, FontStyles.Bold, ConstructorUI.Dorado,
                new Vector2(270f, y), new Vector2(380f, 90f));
            ConstructorUI.CrearBoton(raiz, "+", new Vector2(520f, y), new Vector2(90f, 90f),
                ConstructorUI.Azul, () => AjusteCambiado?.Invoke(campo, 1));
            return texto;
        }
    }
}
