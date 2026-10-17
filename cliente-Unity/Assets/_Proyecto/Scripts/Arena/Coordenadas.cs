using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// El servidor usa origen (0,0) arriba a la izquierda con Y hacia abajo.
    /// El Tilemap de Unity usa Y hacia arriba. Toda conversión pasa por aquí.
    /// </summary>
    public static class Coordenadas
    {
        public static Vector3Int ACelda(int x, int y, int alto) => new Vector3Int(x, alto - 1 - y, 0);

        public static Vector2Int DesdeCelda(Vector3Int celda, int alto) => new Vector2Int(celda.x, alto - 1 - celda.y);
    }
}
