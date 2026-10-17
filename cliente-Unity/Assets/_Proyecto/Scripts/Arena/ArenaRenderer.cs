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

            for (int y = 0; y < escenario.Alto; y++)
            {
                var fila = escenario.Filas[y] ?? "";
                for (int x = 0; x < escenario.Ancho && x < fila.Length; x++)
                    tilemap.SetTile(Coordenadas.ACelda(x, y, alto), TileDe(fila[x]));
            }

            AjustarCamara(escenario.Ancho, escenario.Alto);
            return true;
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
