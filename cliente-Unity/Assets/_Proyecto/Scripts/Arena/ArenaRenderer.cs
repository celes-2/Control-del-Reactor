using System.Collections;
using System.Collections.Generic;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Servicios;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Dibuja el escenario que envía el servidor (US-07). No conoce reglas:
    /// solo traduce cada carácter de las filas a un tile.
    /// </summary>
    public sealed class ArenaRenderer : MonoBehaviour
    {
        [Header("Opcional: si se dejan vacíos se crean automáticamente")]
        [SerializeField] Tilemap tilemap;
        [SerializeField] TileBase tilePiso;
        [SerializeField] TileBase tileMuro;
        [SerializeField] TileBase tileInicio;
        [SerializeField] TileBase tileReactor;

        [Header("Imágenes (opcional): si se dejan vacías se dibujan formas de color")]
        [SerializeField] Sprite spriteReactor;
        [SerializeField] Sprite spriteGuardian;
        [Tooltip("Tamaño del guardián como fracción de la celda (1 = ocupa toda la celda; sube o baja hasta igualar al avatar).")]
        [SerializeField, Range(0.3f, 2f)] float tamanoGuardian = 1f;
        [SerializeField] Sprite spriteTierraMovediza;
        [SerializeField] Sprite spriteCeldaEmergente;
        [SerializeField] Sprite spriteRed;
        [SerializeField] Sprite spritePuertaCerrada;
        [SerializeField] Sprite spritePuertaAbierta;
        [SerializeField] Sprite spriteEnergia;

        [Header("Cámara")]
        [Tooltip("Si el mapa tiene más filas o columnas que esto, la cámara se acerca y sigue a tu avatar. Si no, se ve entero.")]
        [SerializeField, Min(5)] int tamanoMaximoParaVerEntero = 21;
        [Tooltip("Cuántas filas se ven a la vez cuando la cámara sigue al avatar (menos = más cerca).")]
        [SerializeField, Min(5)] int celdasVisibles = 11;
        [Tooltip("Qué tan rápido alcanza la cámara a tu avatar (más alto = más pegada).")]
        [SerializeField] float suavizadoCamara = 6f;
        [SerializeField] float margenCamara = 2f;
        [SerializeField] Color colorFondo = new Color(0.07f, 0.08f, 0.11f);

        int alto;

        // Tema del escenario: colores del mapa y adornos (ver ArteDecorativo y EscenarioArchivo).
        sealed class Paleta
        {
            public Color Piso, PisoBorde, Muro, MuroBorde, Inicio, InicioBorde, Fondo;
        }

        static readonly Paleta PaletaBase = new Paleta
        {
            Piso = new Color(0.17f, 0.19f, 0.25f), PisoBorde = new Color(0.14f, 0.16f, 0.21f),
            Muro = new Color(0.42f, 0.48f, 0.55f), MuroBorde = new Color(0.29f, 0.33f, 0.39f),
            Inicio = new Color(0.18f, 0.36f, 0.31f), InicioBorde = new Color(0.30f, 0.65f, 0.52f),
            Fondo = new Color(0.07f, 0.08f, 0.11f),
        };

        static readonly Dictionary<string, Paleta> Paletas = new Dictionary<string, Paleta>
        {
            ["investigacion"] = new Paleta
            {
                Piso = new Color(0.20f, 0.27f, 0.30f), PisoBorde = new Color(0.16f, 0.22f, 0.25f),
                Muro = new Color(0.62f, 0.74f, 0.80f), MuroBorde = new Color(0.40f, 0.52f, 0.58f),
                Inicio = new Color(0.20f, 0.42f, 0.45f), InicioBorde = new Color(0.40f, 0.85f, 0.85f),
                Fondo = new Color(0.05f, 0.09f, 0.11f),
            },
            ["laboratorio"] = new Paleta
            {
                Piso = new Color(0.13f, 0.22f, 0.17f), PisoBorde = new Color(0.10f, 0.18f, 0.14f),
                Muro = new Color(0.36f, 0.52f, 0.38f), MuroBorde = new Color(0.22f, 0.34f, 0.26f),
                Inicio = new Color(0.20f, 0.40f, 0.26f), InicioBorde = new Color(0.45f, 0.90f, 0.50f),
                Fondo = new Color(0.04f, 0.08f, 0.06f),
            },
            ["estacion"] = new Paleta
            {
                Piso = new Color(0.10f, 0.13f, 0.24f), PisoBorde = new Color(0.08f, 0.10f, 0.20f),
                Muro = new Color(0.42f, 0.46f, 0.64f), MuroBorde = new Color(0.27f, 0.30f, 0.46f),
                Inicio = new Color(0.16f, 0.28f, 0.52f), InicioBorde = new Color(0.40f, 0.75f, 1f),
                Fondo = new Color(0.02f, 0.03f, 0.08f),
            },
            ["militar"] = new Paleta
            {
                Piso = new Color(0.30f, 0.28f, 0.18f), PisoBorde = new Color(0.25f, 0.23f, 0.14f),
                Muro = new Color(0.46f, 0.50f, 0.32f), MuroBorde = new Color(0.30f, 0.33f, 0.20f),
                Inicio = new Color(0.40f, 0.36f, 0.20f), InicioBorde = new Color(0.90f, 0.62f, 0.25f),
                Fondo = new Color(0.10f, 0.09f, 0.05f),
            },
        };

        readonly Dictionary<string, TileBase[]> tilesPorTema = new Dictionary<string, TileBase[]>();
        bool paletaIniciada, tilesDelInspector;
        TileBase inspectorPiso, inspectorMuro, inspectorInicio;
        Color? fondoTema;
        Transform raizDecoracion;
        readonly List<SpriteRenderer> luces = new List<SpriteRenderer>();

        // Cámara que sigue al avatar propio cuando el mapa no cabe en pantalla.
        Transform objetivoCamara;
        bool camaraSigue;
        Vector3 mundoMin, mundoMax;

        // Reactor vivo (Sprint 3): núcleo que pulsa y cambia según energía y estado.
        SpriteRenderer nucleo;
        string estadoReactor = EstadosReactor.Disponible;
        float cargaReactor = 1f;
        float destello;
        int reactorX = -1, reactorY = -1;

        // ---- Arena viva (Sprint 3): guardianes, trampas, puertas e ítems de energía ----
        readonly System.Collections.Generic.List<SpriteRenderer> guardianes = new System.Collections.Generic.List<SpriteRenderer>();
        readonly System.Collections.Generic.List<SpriteRenderer> trampas = new System.Collections.Generic.List<SpriteRenderer>();
        readonly System.Collections.Generic.List<SpriteRenderer> puertas = new System.Collections.Generic.List<SpriteRenderer>();
        readonly System.Collections.Generic.List<SpriteRenderer> items = new System.Collections.Generic.List<SpriteRenderer>();
        readonly System.Collections.Generic.List<Vector3> destinosGuardianes = new System.Collections.Generic.List<Vector3>();
        Sprite cuadroPuerta;

        /// <summary>Dibuja lo que el servidor manda en live. Reutiliza los objetos; los que sobran se ocultan.</summary>
        public void ActualizarArenaViva(ArenaVivaMsg vivo)
        {
            if (vivo == null || alto <= 0) return;

            var gs = vivo.Guardianes ?? new System.Collections.Generic.List<GuardianMsg>();
            AjustarCantidad(guardianes, gs.Count, "Guardian", spriteGuardian != null ? spriteGuardian : GeneradorSprites.Circulo(), 7);
            destinosGuardianes.Clear();
            for (var i = 0; i < gs.Count; i++)
            {
                var g = gs[i];
                var destino = CentroDeCelda(g.X, g.Y);
                destinosGuardianes.Add(destino);
                var sr = guardianes[i];
                if (!sr.gameObject.activeSelf || sr.transform.position == Vector3.zero) sr.transform.position = destino;
                Escalar(sr, g.Temporal ? tamanoGuardian * 0.75f : tamanoGuardian);
                var tinte = g.Pausado ? new Color(0.45f, 0.4f, 0.55f, 0.8f)
                    : g.Temporal ? new Color(1f, 0.45f, 0.75f, 0.95f) : new Color(0.7f, 0.3f, 0.95f, 1f);
                // Con imagen propia solo se atenúa al pausado y se tiñe levemente al temporal.
                sr.color = spriteGuardian == null ? tinte
                    : g.Pausado ? new Color(0.6f, 0.6f, 0.7f, 0.8f) : g.Temporal ? new Color(1f, 0.8f, 0.9f, 1f) : Color.white;
                sr.gameObject.SetActive(true);
            }
            for (var i = gs.Count; i < guardianes.Count; i++) guardianes[i].gameObject.SetActive(false);

            var ts = vivo.Trampas ?? new System.Collections.Generic.List<TrampaMsg>();
            AjustarCantidad(trampas, ts.Count, "Trampa", GeneradorSprites.Circulo(), 2);
            for (var i = 0; i < ts.Count; i++)
            {
                var sr = trampas[i];
                sr.transform.position = CentroDeCelda(ts[i].X, ts[i].Y);
                var imagenTrampa = SpriteTrampa(ts[i].Tipo);
                sr.sprite = imagenTrampa != null ? imagenTrampa : GeneradorSprites.Circulo();
                Escalar(sr, imagenTrampa != null ? 0.9f : 0.62f);
                sr.color = imagenTrampa != null ? Color.white : ColorTrampa(ts[i].Tipo);
                sr.gameObject.SetActive(true);
            }
            for (var i = ts.Count; i < trampas.Count; i++) trampas[i].gameObject.SetActive(false);

            if (cuadroPuerta == null) cuadroPuerta = GeneradorSprites.Cuadro(Color.white, Color.white, 16, 1);
            var ps = vivo.Puertas ?? new System.Collections.Generic.List<PuertaMsg>();
            AjustarCantidad(puertas, ps.Count, "Puerta", cuadroPuerta, 3);
            for (var i = 0; i < ps.Count; i++)
            {
                var sr = puertas[i];
                sr.transform.position = CentroDeCelda(ps[i].X, ps[i].Y);
                var imagenPuerta = ps[i].Abierta ? spritePuertaAbierta : spritePuertaCerrada;
                if (imagenPuerta != null)
                {
                    sr.sprite = imagenPuerta;
                    Escalar(sr, 1f);
                    sr.color = Color.white;
                }
                else
                {
                    sr.sprite = cuadroPuerta;
                    Escalar(sr, ps[i].Abierta ? 0.5f : 0.95f);
                    // Cerrada: bloque ámbar sólido. Abierta: cuadro pequeño y tenue, para que se vea que se puede pasar.
                    sr.color = ps[i].Abierta ? new Color(0.9f, 0.6f, 0.2f, 0.35f) : new Color(0.9f, 0.55f, 0.15f, 1f);
                }
                sr.gameObject.SetActive(true);
            }
            for (var i = ps.Count; i < puertas.Count; i++) puertas[i].gameObject.SetActive(false);

            var its = vivo.Items ?? new System.Collections.Generic.List<ItemEnergiaMsg>();
            AjustarCantidad(items, its.Count, "Energia", spriteEnergia != null ? spriteEnergia : GeneradorSprites.Circulo(), 4);
            for (var i = 0; i < its.Count; i++)
            {
                var sr = items[i];
                sr.transform.position = CentroDeCelda(its[i].X, its[i].Y);
                sr.color = spriteEnergia != null ? Color.white : new Color(0.55f, 1f, 0.45f, 1f);
                sr.gameObject.SetActive(true);
            }
            for (var i = its.Count; i < items.Count; i++) items[i].gameObject.SetActive(false);
        }

        Sprite SpriteTrampa(string tipo)
        {
            switch (tipo)
            {
                case TiposTrampa.TierraMovediza: return spriteTierraMovediza;
                case TiposTrampa.CeldaEmergente: return spriteCeldaEmergente;
                case TiposTrampa.Red: return spriteRed;
                default: return null;
            }
        }

        /// <summary>Ajusta el tamaño para que la imagen ocupe esa fracción de la celda, sea cual sea su resolución.</summary>
        static void Escalar(SpriteRenderer sr, float fraccionDeCelda)
        {
            if (sr.sprite == null) return;
            var medida = sr.sprite.bounds.size;
            var mayor = Mathf.Max(medida.x, medida.y);
            if (mayor <= 0f) return;
            sr.transform.localScale = Vector3.one * (fraccionDeCelda / mayor);
        }

        static Color ColorTrampa(string tipo)
        {
            switch (tipo)
            {
                case TiposTrampa.TierraMovediza: return new Color(0.65f, 0.45f, 0.2f, 0.95f);  // marrón
                case TiposTrampa.CeldaEmergente: return new Color(0.9f, 0.25f, 0.25f, 0.95f);  // rojo
                case TiposTrampa.Red: return new Color(0.85f, 0.95f, 0.85f, 0.9f);              // blanco verdoso
                default: return new Color(1f, 0.6f, 0.2f, 0.9f);
            }
        }

        void AjustarCantidad(System.Collections.Generic.List<SpriteRenderer> lista, int cantidad, string nombre, Sprite sprite, int orden)
        {
            while (lista.Count < cantidad)
            {
                var go = new GameObject(nombre + lista.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = orden;
                lista.Add(sr);
            }
        }

        void LimpiarArenaViva()
        {
            foreach (var lista in new[] { guardianes, trampas, puertas, items })
            {
                foreach (var sr in lista) if (sr != null) Destroy(sr.gameObject);
                lista.Clear();
            }
            destinosGuardianes.Clear();
        }

        public bool Dibujar(EscenarioMsg escenario, string escenarioId = null)
        {
            if (escenario == null || escenario.Filas == null || escenario.Ancho <= 0 || escenario.Filas.Count != escenario.Alto)
            {
                Debug.LogError("Escenario inválido: la cantidad de filas no coincide con el alto.");
                return false;
            }

            AsegurarTilemap();
            var datos = EscenarioArchivo.Buscar(escenarioId);
            AplicarPaleta(datos);

            tilemap.ClearAllTiles();
            alto = escenario.Alto;
            reactorX = reactorY = -1;

            for (int y = 0; y < escenario.Alto; y++)
            {
                var fila = escenario.Filas[y] ?? "";
                for (int x = 0; x < escenario.Ancho && x < fila.Length; x++)
                {
                    tilemap.SetTile(Coordenadas.ACelda(x, y, alto), TileDe(fila[x]));
                    if (fila[x] == Celdas.Reactor) { reactorX = x; reactorY = y; }
                }
            }

            AjustarCamara(escenario.Ancho, escenario.Alto);
            if (fondoTema.HasValue && Camera.main != null) Camera.main.backgroundColor = fondoTema.Value;
            CrearNucleo();
            LimpiarArenaViva();
            DibujarDecoracion(datos, escenario);
            return true;
        }

        /// <summary>Energía y estado del reactor: cambia el tamaño y el color del núcleo.</summary>
        public void ActualizarReactor(int energia, int capacidad, string estado)
        {
            cargaReactor = capacidad > 0 ? Mathf.Clamp01(energia / (float)capacidad) : 1f;
            estadoReactor = string.IsNullOrEmpty(estado) ? EstadosReactor.Disponible : estado;
        }

        /// <summary>Destello breve del reactor cuando alguien extrae.</summary>
        public void Destello() => destello = 1f;

        /// <summary>Rayo de energía desde el reactor hasta el jugador que extrajo.</summary>
        public void LanzarRayo(Vector3 hacia)
        {
            if (nucleo == null) return;
            StartCoroutine(RutinaRayo(nucleo.transform.position, hacia));
        }

        IEnumerator RutinaRayo(Vector3 desde, Vector3 hasta)
        {
            var go = new GameObject("RayoExtraccion");
            go.transform.SetParent(transform, false);
            var linea = go.AddComponent<LineRenderer>();
            linea.material = new Material(Shader.Find("Sprites/Default"));
            linea.positionCount = 2;
            linea.useWorldSpace = true;
            linea.numCapVertices = 4;
            linea.sortingOrder = 9;
            linea.SetPosition(0, desde);
            linea.SetPosition(1, hasta);

            const float duracion = 0.3f;
            for (float t = 0f; t < duracion; t += Time.deltaTime)
            {
                float k = 1f - t / duracion;
                linea.startWidth = 0.22f * k;
                linea.endWidth = 0.06f * k;
                var c = new Color(1f, 0.92f, 0.35f, k);
                linea.startColor = c;
                linea.endColor = c;
                yield return null;
            }
            Destroy(go);
        }

        void CrearNucleo()
        {
            if (nucleo != null) Destroy(nucleo.gameObject);
            nucleo = null;
            if (reactorX < 0) return;

            var go = new GameObject("NucleoReactor");
            go.transform.SetParent(transform, false);
            go.transform.position = CentroDeCelda(reactorX, reactorY);
            nucleo = go.AddComponent<SpriteRenderer>();
            nucleo.sprite = spriteReactor != null ? spriteReactor : GeneradorSprites.Circulo();
            nucleo.sortingOrder = 5; // sobre el suelo, debajo de los avatares
            cargaReactor = 1f;
            estadoReactor = EstadosReactor.Disponible;
        }

        void Update()
        {
            // Los guardianes se deslizan hacia su celda en vez de saltar (el servidor los mueve cada segundo).
            for (var i = 0; i < guardianes.Count && i < destinosGuardianes.Count; i++)
                if (guardianes[i] != null && guardianes[i].gameObject.activeSelf)
                    guardianes[i].transform.position = Vector3.MoveTowards(
                        guardianes[i].transform.position, destinosGuardianes[i], 6f * Time.deltaTime);

            // Las luces decorativas (emergencia, balizas) parpadean.
            var parpadeo = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 3f));
            foreach (var luz in luces)
                if (luz != null) luz.color = new Color(1f, 1f, 1f, parpadeo);

            // Las energías sueltas laten para que se noten.
            var latido = 0.38f + 0.06f * Mathf.Sin(Time.time * 6f);
            foreach (var sr in items)
                if (sr != null && sr.gameObject.activeSelf) Escalar(sr, latido);

            if (nucleo == null) return;

            destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 4f);
            float pulso = Mathf.Sin(Time.time * 3f);

            float tamano;
            Color color;
            switch (estadoReactor)
            {
                case EstadosReactor.Desactivado:
                    // Sobrecarga: rojo, parpadeando rápido.
                    tamano = 0.55f;
                    color = new Color(0.95f, 0.2f, 0.2f, 0.45f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 8f)));
                    break;
                case EstadosReactor.Vacio:
                    // Vacío: gris apagado, sin pulso.
                    tamano = 0.3f;
                    color = new Color(0.5f, 0.52f, 0.58f, 0.7f);
                    break;
                default:
                    // Activo: más grande y brillante mientras más energía; pulsa suave.
                    tamano = 0.4f + 0.35f * cargaReactor + 0.04f * pulso;
                    color = Color.Lerp(new Color(0.9f, 0.45f, 0.1f), new Color(1f, 0.95f, 0.4f), cargaReactor);
                    break;
            }

            tamano += 0.25f * destello;
            color = Color.Lerp(color, Color.white, 0.6f * destello);
            // Con imagen propia el estado se nota por el tamaño y un tinte suave, no por teñirla entera.
            if (spriteReactor != null) color = Color.Lerp(Color.white, color, 0.35f);
            Escalar(nucleo, tamano);
            nucleo.color = color;
        }

        public Vector3 CentroDeCelda(int x, int y)
        {
            AsegurarTilemap();
            return tilemap.GetCellCenterWorld(Coordenadas.ACelda(x, y, alto));
        }

        TileBase TileDe(char celda)
        {
            switch (celda)
            {
                case Celdas.Muro: return tileMuro;
                case Celdas.Inicio: return tileInicio;
                case Celdas.Reactor: return tileReactor;
                default: return tilePiso;
            }
        }

        void AsegurarTilemap()
        {
            if (tilemap != null) return;

            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(transform, false);

            var capa = new GameObject("Suelo", typeof(Tilemap), typeof(TilemapRenderer));
            capa.transform.SetParent(grid.transform, false);
            tilemap = capa.GetComponent<Tilemap>();
            capa.GetComponent<TilemapRenderer>().sortingOrder = 0;
        }

        /// <summary>Elige los colores del suelo y las paredes según el tema (si no hay tiles puestos a mano en el Inspector).</summary>
        void AplicarPaleta(EscenarioArchivo datos)
        {
            if (!paletaIniciada)
            {
                // Los tiles puestos a mano en el Inspector se guardan aparte: solo se usan para escenarios sin archivo propio.
                paletaIniciada = true;
                inspectorPiso = tilePiso;
                inspectorMuro = tileMuro;
                inspectorInicio = tileInicio;
                tilesDelInspector = tilePiso != null || tileMuro != null || tileInicio != null;
            }
            if (tilesDelInspector && datos == null)
            {
                tilePiso = inspectorPiso;
                tileMuro = inspectorMuro;
                tileInicio = inspectorInicio;
                AsegurarTiles();
                fondoTema = null;
                return;
            }

            var tema = datos?.Tema;
            var clave = tema != null && Paletas.ContainsKey(tema) ? tema : "";
            var p = clave == "" ? PaletaBase : Paletas[clave];

            // Los tiles son los mismos mientras no cambien el tema ni las imágenes pedidas.
            var claveTiles = clave + "|" + datos?.TilePiso + "|" + datos?.TilePared;
            if (!tilesPorTema.TryGetValue(claveTiles, out var tiles))
            {
                var imagenPiso = EscenarioArchivo.CargarImagenTile(datos?.TilePiso);
                var pisoOriginal = datos?.TilePiso == EscenarioArchivo.TileOriginal && inspectorPiso != null;
                var imagenPared = EscenarioArchivo.CargarImagenTile(datos?.TilePared);
                tiles = new[]
                {
                    pisoOriginal ? inspectorPiso : imagenPiso != null ? CrearTileImagen(imagenPiso, Color.white) : CrearTile(p.Piso, p.PisoBorde),
                    imagenPared != null ? CrearTileImagen(imagenPared, Color.white) : CrearTile(p.Muro, p.MuroBorde),
                    // La salida de jugador es el piso con un tinte, para que se siga distinguiendo.
                    pisoOriginal && inspectorInicio != null ? inspectorInicio
                        : imagenPiso != null ? CrearTileImagen(imagenPiso, Color.Lerp(Color.white, p.InicioBorde, 0.6f)) : CrearTile(p.Inicio, p.InicioBorde),
                };
                tilesPorTema[claveTiles] = tiles;
            }
            tilePiso = tiles[0];
            tileMuro = tiles[1];
            tileInicio = tiles[2];
            fondoTema = p.Fondo;
            AsegurarTiles(); // completa el del reactor si falta
        }

        /// <summary>Tile con una imagen propia: una celda mide una unidad sin importar el tamaño de la imagen.</summary>
        static TileBase CrearTileImagen(Texture2D imagen, Color tinte)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(imagen, new Rect(0, 0, imagen.width, imagen.height), new Vector2(0.5f, 0.5f), imagen.width);
            tile.color = tinte;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        void LimpiarDecoracion()
        {
            luces.Clear();
            if (raizDecoracion == null) return;
            Destroy(raizDecoracion.gameObject);
            raizDecoracion = null;
        }

        /// <summary>
        /// Adornos del tema sobre el piso (solo visuales: no bloquean ni cambian nada del juego).
        /// Solo se dibujan si el mapa del servidor mide lo mismo que el del archivo, y solo sobre celdas de piso.
        /// </summary>
        void DibujarDecoracion(EscenarioArchivo datos, EscenarioMsg escenario)
        {
            LimpiarDecoracion();
            if (datos == null || datos.Ancho != escenario.Ancho || datos.Alto != escenario.Alto) return;

            raizDecoracion = new GameObject("Decoracion").transform;
            raizDecoracion.SetParent(transform, false);
            for (var y = 0; y < datos.Alto; y++)
                for (var x = 0; x < datos.Ancho; x++)
                {
                    var tipo = datos.Decoracion[y][x];
                    if (tipo == '.' || escenario.Filas[y].Length <= x || escenario.Filas[y][x] != Celdas.Piso) continue;
                    var sprite = ArteDecorativo.Para(datos.Tema, tipo);
                    if (sprite == null) continue;

                    var go = new GameObject($"Adorno_{tipo}_{x}_{y}");
                    go.transform.SetParent(raizDecoracion, false);
                    go.transform.position = CentroDeCelda(x, y);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 1; // sobre el piso, debajo de trampas, puertas y avatares
                    Escalar(sr, 0.78f);
                    if (ArteDecorativo.Parpadea(datos.Tema, tipo)) luces.Add(sr);
                }
        }

        void AsegurarTiles()
        {
            if (tilePiso == null) tilePiso = CrearTile(new Color(0.17f, 0.19f, 0.25f), new Color(0.14f, 0.16f, 0.21f));
            if (tileMuro == null) tileMuro = CrearTile(new Color(0.42f, 0.48f, 0.55f), new Color(0.29f, 0.33f, 0.39f));
            if (tileInicio == null) tileInicio = CrearTile(new Color(0.18f, 0.36f, 0.31f), new Color(0.30f, 0.65f, 0.52f));
            if (tileReactor == null) tileReactor = CrearTile(new Color(0.95f, 0.77f, 0.06f), new Color(0.90f, 0.49f, 0.13f), 4);
        }

        static TileBase CrearTile(Color relleno, Color borde, int grosor = 2)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = GeneradorSprites.Cuadro(relleno, borde, 32, grosor);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        /// <summary>La cámara sigue a este objeto cuando el mapa es más grande que la pantalla.</summary>
        public void SeguirA(Transform objetivo) => objetivoCamara = objetivo;

        void LateUpdate()
        {
            if (!camaraSigue || objetivoCamara == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            float mitadAlto = cam.orthographicSize;
            float mitadAncho = mitadAlto * cam.aspect;
            var meta = objetivoCamara.position;
            // No mostrar nada fuera del mapa; si el mapa es más chico que la vista en un eje, se centra.
            meta.x = mundoMax.x - mundoMin.x <= mitadAncho * 2f ? (mundoMin.x + mundoMax.x) / 2f
                : Mathf.Clamp(meta.x, mundoMin.x + mitadAncho, mundoMax.x - mitadAncho);
            meta.y = mundoMax.y - mundoMin.y <= mitadAlto * 2f ? (mundoMin.y + mundoMax.y) / 2f
                : Mathf.Clamp(meta.y, mundoMin.y + mitadAlto, mundoMax.y - mitadAlto);
            meta.z = cam.transform.position.z;
            cam.transform.position = Vector3.Lerp(cam.transform.position, meta, 1f - Mathf.Exp(-suavizadoCamara * Time.deltaTime));
        }

        void AjustarCamara(int ancho, int altoMapa)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("No hay una cámara con la etiqueta MainCamera en la escena.");
                return;
            }

            var min = tilemap.CellToWorld(Vector3Int.zero);
            var max = tilemap.CellToWorld(new Vector3Int(ancho, altoMapa, 0));
            var centro = (min + max) / 2f;
            mundoMin = min;
            mundoMax = max;

            // Mapa grande: vista cercana de ~celdasVisibles filas y la cámara sigue al avatar propio.
            camaraSigue = Mathf.Max(ancho, altoMapa) > tamanoMaximoParaVerEntero;
            if (camaraSigue)
            {
                cam.orthographic = true;
                cam.orthographicSize = celdasVisibles / 2f + 0.5f;
                cam.backgroundColor = colorFondo;
                if (objetivoCamara != null)
                {
                    var p = objetivoCamara.position;
                    cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
                }
                else cam.transform.position = new Vector3(centro.x, centro.y, cam.transform.position.z);
                return;
            }

            cam.orthographic = true;
            cam.transform.position = new Vector3(centro.x, centro.y, cam.transform.position.z);
            float porAlto = (max.y - min.y) / 2f + margenCamara;
            float porAncho = ((max.x - min.x) / 2f + margenCamara) / cam.aspect;
            cam.orthographicSize = Mathf.Max(porAlto, porAncho);
            cam.backgroundColor = colorFondo;
        }
    }
}
