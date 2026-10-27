using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    public enum Pista
    {
        Ninguna,
        Menu,     // configuración y sala de espera
        Arena,    // durante la partida (CA-09.10)
        Victoria, // pantalla de victoria (CA-17.4)
    }

    /// <summary>
    /// Reproduce la música de fondo. Vive en el objeto CompositionRoot de la escena Arranque, así que no se
    /// corta al cambiar de escena. Las pistas se arrastran en el Inspector; si una está vacía, esa pantalla
    /// queda en silencio (sirve mientras todavía no se tienen los archivos).
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        [Header("Pistas (arrastrar desde Assets/_Proyecto/Audio)")]
        [SerializeField] AudioClip musicaMenu;
        [SerializeField] AudioClip musicaArena;
        [SerializeField] AudioClip musicaVictoria;

        [Header("Ajustes")]
        [SerializeField, Range(0f, 1f)] float volumen = 0.5f;
        [SerializeField, Tooltip("La música de victoria suena una sola vez si está desactivado.")] bool repetirVictoria = true;

        AudioSource fuente;
        Pista actual = Pista.Ninguna;

        void Awake()
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f; // sonido 2D
            fuente.volume = volumen;
        }

        /// <summary>Cambia la música. Si ya está sonando esa pista no se reinicia.</summary>
        public void Reproducir(Pista pista)
        {
            if (pista == actual && fuente.isPlaying) return;
            actual = pista;

            var clip = ClipDe(pista);
            if (clip == null)
            {
                fuente.Stop();
                return;
            }

            fuente.clip = clip;
            fuente.loop = pista != Pista.Victoria || repetirVictoria;
            fuente.volume = volumen;
            fuente.Play();
        }

        public void Detener()
        {
            actual = Pista.Ninguna;
            fuente.Stop();
        }

        public void CambiarVolumen(float nuevo)
        {
            volumen = Mathf.Clamp01(nuevo);
            if (fuente != null) fuente.volume = volumen;
        }

        AudioClip ClipDe(Pista pista)
        {
            switch (pista)
            {
                case Pista.Menu: return musicaMenu;
                case Pista.Arena: return musicaArena;
                case Pista.Victoria: return musicaVictoria;
                default: return null;
            }
        }
    }
}
