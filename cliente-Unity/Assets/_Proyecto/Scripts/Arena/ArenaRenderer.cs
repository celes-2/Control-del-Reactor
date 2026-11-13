using System.Collections;
using Reactor.Cliente.Contratos;
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

        [Header("Cámara")]
        [SerializeField] float margenCamara = 2f;
        [SerializeField] Color colorFondo = new Color(0.07f, 0.08f, 0.11f);

        int alto;

        // Reactor vivo (Sprint 3): núcleo que pulsa y cambia según energía y estado.
        SpriteRenderer nucleo;
        string estadoReactor = EstadosReactor.Disponible;
        float cargaReactor = 1f;
        float destello;
        int reactorX = -1, reactorY = -1;

        public bool Dibujar(EscenarioMsg escenario)
        {
            if (escenario == null || escenario.Filas == null || escenario.Ancho <= 0 || escenario.Filas.Count != escenario.Alto)
            {
                Debug.LogError("Escenario inválido: la cantidad de filas no coincide con el alto.");
                return false;
            }

            AsegurarTilemap();
            AsegurarTiles();

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
            CrearNucleo();
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
            nucleo.sprite = GeneradorSprites.Circulo();
            nucleo.sortingOrder = 5; // sobre el suelo, debajo de los avatares
            cargaReactor = 1f;
            estadoReactor = EstadosReactor.Disponible;
        }

        void Update()
        {
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
            nucleo.transform.localScale = Vector3.one * tamano;
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

            cam.orthographic = true;
            cam.transform.position = new Vector3(centro.x, centro.y, cam.transform.position.z);
            float porAlto = (max.y - min.y) / 2f + margenCamara;
            float porAncho = ((max.x - min.x) / 2f + margenCamara) / cam.aspect;
            cam.orthographicSize = Mathf.Max(porAlto, porAncho);
            cam.backgroundColor = colorFondo;
        }
    }
}
