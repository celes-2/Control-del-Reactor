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

        SpriteRenderer cuerpo;
        TextMeshPro inicial;
        TextMeshPro burbuja;
        Coroutine rutinaBurbuja;
        Vector3 destino;

        public string JugadorId { get; private set; }

        public static AvatarVista Crear(Transform padre, JugadorMsg jugador, Vector3 posicion)
        {
            var go = new GameObject($"Avatar_{jugador.Nombre}");
            go.transform.SetParent(padre, false);
            go.transform.position = posicion;

            var avatar = go.AddComponent<AvatarVista>();
            avatar.Construir(jugador, posicion);
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
            inicial.gameObject.SetActive(false);
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, destino, Velocidad * Time.deltaTime);
        }

        void Construir(JugadorMsg jugador, Vector3 posicion)
        {
            JugadorId = jugador.Id;
            destino = posicion;

            var color = Colores.Desde(jugador.Color, Color.white);
            var circulo = GeneradorSprites.Circulo();

            CrearSprite("Anillo", circulo, color, 0.95f, 10);
            cuerpo = CrearSprite("Cuerpo", circulo, Color.Lerp(color, Color.white, 0.55f), 0.72f, 11);
            inicial = CrearTexto("Inicial", Inicial(jugador.Nombre), Vector3.zero, 5f, new Color(0.1f, 0.1f, 0.12f), 12, false, new Vector2(1f, 1f));
            CrearTexto("Nombre", jugador.Nombre, new Vector3(0f, 0.75f, 0f), 3f, Color.white, 13, true, new Vector2(4f, 0.6f));
            burbuja = CrearTexto("Burbuja", "", new Vector3(0f, 1.3f, 0f), 3.5f, new Color(1f, 0.92f, 0.3f), 14, true, new Vector2(6f, 0.8f));
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
