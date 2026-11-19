using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Reactor.Cliente.Contratos;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Un escenario definido en un archivo de StreamingAssets/Escenarios/*.json. Se puede editar a mano:
    /// el servidor falso lo juega tal cual, la pantalla de Configuración lo muestra como vista previa y la arena
    /// dibuja su decoración temática.
    ///
    /// Símbolos de "filas" (todas del mismo largo; el borde debe ser pared):
    ///   W pared · . piso · S salida de jugador · R reactor (uno solo) · D puerta (se abre y cierra)
    ///   G salida de guardián · T trampa (el tipo va rotando) · Q tierra movediza · C celda emergente · N red
    ///   E punto donde puede aparecer energía
    /// Símbolos de "decoracion" (mismo tamaño; '.' = nada): a, b, c, d = los cuatro adornos del "tema".
    /// Temas: investigacion, laboratorio, estacion, militar.
    /// </summary>
    public sealed class EscenarioArchivo
    {
        public string Id { get; private set; }
        public string Nombre { get; private set; }
        public string Descripcion { get; private set; }
        public string Tema { get; private set; }

        /// <summary>Imagen del piso y de las paredes (archivos de StreamingAssets/Escenarios/Tiles). Vacío = colores del tema.</summary>
        public string TilePiso { get; private set; }
        public string TilePared { get; private set; }
        public int Ancho { get; private set; }
        public int Alto { get; private set; }

        /// <summary>Filas en el formato que manda el servidor: solo W . S R (lo demás se dibuja encima).</summary>
        public string[] FilasJuego { get; private set; }

        /// <summary>Adornos por celda: '.' = nada, a-d = tipo de adorno del tema. Mismo tamaño que FilasJuego.</summary>
        public string[] Decoracion { get; private set; }

        public List<(int x, int y)> Puertas { get; } = new List<(int, int)>();
        public List<(int x, int y)> InicioGuardianes { get; } = new List<(int, int)>();
        public List<(int x, int y)> PuntosEnergia { get; } = new List<(int, int)>();
        public List<(int x, int y, string tipo)> Trampas { get; } = new List<(int, int, string)>();

        /// <summary>Problemas encontrados al leerlo (se muestran como advertencias en la consola de Unity).</summary>
        public List<string> Avisos { get; } = new List<string>();

        static readonly string[] OrdenConocido = { "centro-investigacion", "laboratorio", "estacion", "militar" };
        static readonly string[] TiposRotativos = { TiposTrampa.TierraMovediza, TiposTrampa.CeldaEmergente, TiposTrampa.Red };

        public static string Carpeta => Path.Combine(Application.streamingAssetsPath, "Escenarios");

        /// <summary>Todos los escenarios de la carpeta, primero los conocidos en su orden y luego el resto por nombre.</summary>
        public static List<EscenarioArchivo> CargarTodos()
        {
            var lista = new List<EscenarioArchivo>();
            try
            {
                if (!Directory.Exists(Carpeta)) return lista;
                var archivos = Directory.GetFiles(Carpeta, "*.json")
                    .OrderBy(a =>
                    {
                        var i = Array.IndexOf(OrdenConocido, Path.GetFileNameWithoutExtension(a));
                        return i < 0 ? int.MaxValue : i;
                    })
                    .ThenBy(a => a, StringComparer.OrdinalIgnoreCase);
                foreach (var archivo in archivos)
                {
                    var e = Leer(archivo);
                    if (e != null) lista.Add(e);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("No se pudieron leer los escenarios: " + ex.Message);
            }
            return lista;
        }

        public const string TileOriginal = "original";

        public static string CarpetaTiles => Path.Combine(Carpeta, "Tiles");

        /// <summary>Imagen (png o jpg) de CarpetaTiles; null si no se pidió o no existe. Se lee de nuevo cada vez.</summary>
        public static Texture2D CargarImagenTile(string nombre)
        {
            // "original" = el tile de piso de la escena de la arena (no es un archivo).
            if (string.IsNullOrWhiteSpace(nombre) || nombre == TileOriginal) return null;
            try
            {
                var ruta = Path.Combine(CarpetaTiles, Path.GetFileName(nombre));
                if (!File.Exists(ruta))
                {
                    Debug.LogWarning($"No se encontró el tile '{nombre}' en {CarpetaTiles}; se usan los colores del tema.");
                    return null;
                }
                var textura = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                return textura.LoadImage(File.ReadAllBytes(ruta)) ? textura : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"No se pudo leer el tile '{nombre}': {ex.Message}");
                return null;
            }
        }

        /// <summary>Color medio de un tile, para la vista previa de Configuración.</summary>
        public static Color? ColorPromedio(string nombre)
        {
            var textura = CargarImagenTile(nombre);
            if (textura == null) return null;
            var pixeles = textura.GetPixels32();
            UnityEngine.Object.Destroy(textura);
            if (pixeles.Length == 0) return null;
            long r = 0, g = 0, b = 0;
            foreach (var p in pixeles) { r += p.r; g += p.g; b += p.b; }
            return new Color(r / (255f * pixeles.Length), g / (255f * pixeles.Length), b / (255f * pixeles.Length));
        }

        /// <summary>El escenario con ese id; si no existe (o es vacío), el primero de la carpeta. Null si no hay ninguno.</summary>
        public static EscenarioArchivo Cargar(string id)
        {
            var todos = CargarTodos();
            if (todos.Count == 0) return null;
            return todos.FirstOrDefault(e => e.Id == id) ?? todos[0];
        }

        /// <summary>Solo lee el que tenga ese id (para el tema y la decoración); null si no existe.</summary>
        public static EscenarioArchivo Buscar(string id) =>
            string.IsNullOrEmpty(id) ? null : CargarTodos().FirstOrDefault(e => e.Id == id);

        /// <summary>Para cuando no hay archivos: un escenario armado con filas ya listas.</summary>
        public static EscenarioArchivo DeFilas(string id, string nombre, IReadOnlyList<string> filas)
        {
            var e = new EscenarioArchivo { Id = id, Nombre = nombre, Descripcion = "", Tema = "investigacion" };
            e.Analizar(filas, null);
            return e;
        }

        static EscenarioArchivo Leer(string ruta)
        {
            var nombreArchivo = Path.GetFileName(ruta);
            try
            {
                var json = JObject.Parse(File.ReadAllText(ruta));
                var id = (string)json["id"] ?? Path.GetFileNameWithoutExtension(ruta);
                var e = new EscenarioArchivo
                {
                    Id = id,
                    Nombre = (string)json["nombre"] ?? id,
                    Descripcion = (string)json["descripcion"] ?? "",
                    Tema = ((string)json["tema"] ?? "investigacion").ToLowerInvariant(),
                    TilePiso = (string)json["tiles"]?["piso"] ?? "",
                    TilePared = (string)json["tiles"]?["pared"] ?? "",
                };
                var filas = (json["filas"] as JArray)?.Select(f => (string)f ?? "").ToList() ?? new List<string>();
                var decoracion = (json["decoracion"] as JArray)?.Select(f => (string)f ?? "").ToList();
                e.Analizar(filas, decoracion);
                foreach (var aviso in e.Avisos) Debug.LogWarning($"Escenario '{nombreArchivo}': {aviso}");
                return e.FilasJuego.Length == 0 ? null : e;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Escenario '{nombreArchivo}' inválido: {ex.Message}");
                return null;
            }
        }

        void Analizar(IReadOnlyList<string> filas, IReadOnlyList<string> decoracion)
        {
            Alto = filas.Count;
            Ancho = filas.Count == 0 ? 0 : filas.Max(f => f.Length);
            var juego = new string[Alto];
            var reactores = 0;
            var salidas = 0;

            for (var y = 0; y < Alto; y++)
            {
                if (filas[y].Length != Ancho) Avisos.Add($"la fila {y} mide {filas[y].Length} y debería medir {Ancho}.");
                var fila = new char[Ancho];
                for (var x = 0; x < Ancho; x++)
                {
                    var c = x < filas[y].Length ? char.ToUpperInvariant(filas[y][x]) : 'W';
                    switch (c)
                    {
                        case 'R': reactores++; fila[x] = Celdas.Reactor; break;
                        case 'S': salidas++; fila[x] = Celdas.Inicio; break;
                        case 'W': fila[x] = Celdas.Muro; break;
                        case 'D': Puertas.Add((x, y)); fila[x] = Celdas.Piso; break;
                        case 'G': InicioGuardianes.Add((x, y)); fila[x] = Celdas.Piso; break;
                        case 'E': PuntosEnergia.Add((x, y)); fila[x] = Celdas.Piso; break;
                        case 'T': Trampas.Add((x, y, TiposRotativos[Trampas.Count % TiposRotativos.Length])); fila[x] = Celdas.Piso; break;
                        case 'Q': Trampas.Add((x, y, TiposTrampa.TierraMovediza)); fila[x] = Celdas.Piso; break;
                        case 'C': Trampas.Add((x, y, TiposTrampa.CeldaEmergente)); fila[x] = Celdas.Piso; break;
                        case 'N': Trampas.Add((x, y, TiposTrampa.Red)); fila[x] = Celdas.Piso; break;
                        default: fila[x] = Celdas.Piso; break;
                    }
                }
                juego[y] = new string(fila);
            }
            FilasJuego = juego;

            if (reactores != 1) Avisos.Add($"debe haber exactamente un reactor (R) y hay {reactores}.");
            if (salidas < 2) Avisos.Add($"hacen falta al menos 2 salidas de jugador (S) y hay {salidas}.");
            if (Alto > 0 && (juego[0].Any(c => c != Celdas.Muro) || juego[Alto - 1].Any(c => c != Celdas.Muro)
                             || juego.Any(f => f.Length > 0 && (f[0] != Celdas.Muro || f[f.Length - 1] != Celdas.Muro))))
                Avisos.Add("el borde del mapa debería ser todo pared (W).");

            var deco = new string[Alto];
            for (var y = 0; y < Alto; y++)
            {
                var origen = decoracion != null && y < decoracion.Count ? decoracion[y] : "";
                deco[y] = origen.PadRight(Ancho, '.').Substring(0, Ancho);
            }
            Decoracion = deco;
        }

        /// <summary>Lo que necesita la pantalla de Configuración para dibujar la vista previa.</summary>
        public EscenarioInfo AInfo()
        {
            var adornos = new List<(int x, int y)>();
            for (var y = 0; y < Alto; y++)
                for (var x = 0; x < Ancho; x++)
                    if (Decoracion[y][x] != '.') adornos.Add((x, y));

            // La vista previa pinta las trampas con la letra T, las energías con E, etc.
            var vista = new string[Alto];
            for (var y = 0; y < Alto; y++)
            {
                var fila = FilasJuego[y].ToCharArray();
                vista[y] = new string(fila);
            }

            return new EscenarioInfo
            {
                Id = Id,
                Nombre = Nombre,
                Descripcion = Descripcion,
                Tema = Tema,
                TilePiso = TilePiso,
                TilePared = TilePared,
                Filas = vista,
                Puertas = Puertas.ToList(),
                InicioGuardianes = InicioGuardianes.ToList(),
                Trampas = Trampas.Select(t => (t.x, t.y)).ToList(),
                PuntosEnergia = PuntosEnergia.ToList(),
                Decoracion = adornos,
                MaxJugadores = Math.Min(4, FilasJuego.Sum(f => f.Count(c => c == Celdas.Inicio))),
                MinGuardianes = 2,
                MaxGuardianes = 4,
                GuardianesPorDefecto = 4,
            };
        }
    }
}
