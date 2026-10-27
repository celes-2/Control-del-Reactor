using System.Collections;
using Reactor.Cliente.Contratos;
using TMPro;
using UnityEngine;

namespace Reactor.Cliente.Arena
{
    /// <summary>
    /// Representación de un jugador en la arena: anillo con su color (para distinguirlo),
    /// cuerpo (luego, la imagen del avatar), nombre y burbuja para la acción (US-14).
    /// Se crea con la fábrica Crear(...).
    /// </summary>
    public sealed class AvatarVista : MonoBehaviour
    {
        const float Velocidad = 8f;          // unidades por segundo: movimiento perceptible (CA-11.2)
        const float DuracionBurbuja = 1.5f;
        const float TamanoAnillo = 0.95f;    // fracción de la celda que ocupa el anillo con escala 1

        SpriteRenderer cuerpo;
        TextMeshPro inicial;
        TextMeshPro burbuja;
        Coroutine rutinaBurbuja;
        Vector3 destino;
        float tamanoCuerpo;                  // tamaño final de la imagen, en unidades

        public string JugadorId { get; private set; }

        /// <param name="escala">1 = el anillo llena casi toda la celda.</param>
        /// <param name="proporcionImagen">Tamaño de la imagen respecto al anillo (más de 1 = sobresale).</param>
        public static AvatarVista Crear(Transform padre, JugadorMsg jugador, Vector3 posicion,
                                        float escala = 1f, float proporcionImagen = 0.76f)
        {
            var go = new GameObject($"Avatar_{jugador.Nombre}");
            go.transform.SetParent(padre, false);
            go.transform.position = posicion;

            var avatar = go.AddComponent<AvatarVista>();
            avatar.Construir(jugador, posicion, Mathf.Max(0.1f, escala), Mathf.Max(0.1f, proporcionImagen));
            return avatar;
        }

        public void Colocar(Vector3 posicion)
        {
            transform.position = posicion;
            destino = posicion;
        }

        public void MoverA(Vector3 posicion) => destino = posicion;

        public void MostrarMensaje(string mensaje)
        {
            if (rutinaBurbuja != null) StopCoroutine(rutinaBurbuja);
            rutinaBurbuja = StartCoroutine(RutinaBurbuja(mensaje));
        }

        /// <summary>Para cuando se descargue la imagen real del avatar.</summary>
        public void PonerImagen(Sprite imagen)
        {
            if (imagen == null) return;
            cuerpo.sprite = imagen;
            cuerpo.color = Color.white;

            // Ajusta cualquier tamaño de imagen para que quepa dentro del anillo de color.
            var tamano = imagen.bounds.size;
            var mayor = Mathf.Max(tamano.x, tamano.y);
            if (mayor > 0f) cuerpo.transform.localScale = Vector3.one * (tamanoCuerpo / mayor);

            inicial.gameObject.SetActive(false);
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, destino, Velocidad * Time.deltaTime);
        }

        void Construir(JugadorMsg jugador, Vector3 posicion, float escala, float proporcionImagen)
        {
            JugadorId = jugador.Id;
            destino = posicion;

            float anillo = TamanoAnillo * escala;
            tamanoCuerpo = anillo * proporcionImagen;
            float borde = Mathf.Max(anillo, tamanoCuerpo) / 2f;   // dónde termina el avatar hacia arriba

            var color = Colores.Desde(jugador.Color, Color.white);
            var circulo = GeneradorSprites.Circulo();

            CrearSprite("Anillo", circulo, color, anillo, 10);
            cuerpo = CrearSprite("Cuerpo", circulo, Color.Lerp(color, Color.white, 0.55f), tamanoCuerpo, 11);
            inicial = CrearTexto("Inicial", Inicial(jugador.Nombre), Vector3.zero, 5f * escala, new Color(0.1f, 0.1f, 0.12f), 12, false, new Vector2(anillo, anillo));
            CrearTexto("Nombre", jugador.Nombre, new Vector3(0f, borde + 0.25f, 0f), 3f, Color.white, 13, true, new Vector2(4f, 0.6f));
            burbuja = CrearTexto("Burbuja", "", new Vector3(0f, borde + 0.8f, 0f), 3.5f, new Color(1f, 0.92f, 0.3f), 14, true, new Vector2(6f, 0.8f));
            burbuja.gameObject.SetActive(false);
        }

        IEnumerator RutinaBurbuja(string mensaje)
        {
            burbuja.text = mensaje;
            burbuja.gameObject.SetActive(true);

            const float entrada = 0.12f;
            for (float t = 0f; t < entrada; t += Time.deltaTime)
            {
                burbuja.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, t / entrada);
                yield return null;
            }
            burbuja.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(DuracionBurbuja);
            burbuja.gameObject.SetActive(false);
            rutinaBurbuja = null;
        }

        SpriteRenderer CrearSprite(string nombre, Sprite sprite, Color color, float escala, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * escala;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = orden;
            return sr;
        }

        TextMeshPro CrearTexto(string nombre, string texto, Vector3 posicion, float tamano, Color color, int orden, bool contorno, Vector2 caja)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = posicion;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = caja;
            tmp.text = texto;
            tmp.fontSize = tamano;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = orden;
            if (contorno)
            {
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(0, 0, 0, 255);
            }
            return tmp;
        }

        static string Inicial(string nombre) =>
            string.IsNullOrWhiteSpace(nombre) ? "?" : nombre.Trim().Substring(0, 1).ToUpperInvariant();
    }
}
