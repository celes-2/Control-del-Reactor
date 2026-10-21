using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Dónde se recuerdan los ajustes de la última partida (CA-08.1). Es una interfaz para poder probar
    /// el presentador sin PlayerPrefs.
    /// </summary>
    public interface IAlmacenAjustes
    {
        /// <summary>Los ajustes guardados, o los valores por defecto si todavía no hay.</summary>
        AjustesPartida Cargar();

        void Guardar(AjustesPartida ajustes);
    }

    /// <summary>Guarda en PlayerPrefs del equipo (queda aunque se cierre el juego).</summary>
    public sealed class AlmacenAjustesPlayerPrefs : IAlmacenAjustes
    {
        const string Prefijo = "ajustes.";

        public AjustesPartida Cargar()
        {
            var porDefecto = new AjustesPartida();
            var ajustes = new AjustesPartida
            {
                Modo = (ModoFin)PlayerPrefs.GetInt(Prefijo + "modo", (int)porDefecto.Modo),
                DuracionSegundos = PlayerPrefs.GetInt(Prefijo + "duracion", porDefecto.DuracionSegundos),
                PuntosObjetivo = PlayerPrefs.GetInt(Prefijo + "puntos", porDefecto.PuntosObjetivo),
                SegundosCongelamiento = PlayerPrefs.GetInt(Prefijo + "congelamiento", porDefecto.SegundosCongelamiento),
                MaxJugadores = PlayerPrefs.GetInt(Prefijo + "jugadores", porDefecto.MaxJugadores),
            };
            ajustes.Normalizar();
            return ajustes;
        }

        public void Guardar(AjustesPartida ajustes)
        {
            PlayerPrefs.SetInt(Prefijo + "modo", (int)ajustes.Modo);
            PlayerPrefs.SetInt(Prefijo + "duracion", ajustes.DuracionSegundos);
            PlayerPrefs.SetInt(Prefijo + "puntos", ajustes.PuntosObjetivo);
            PlayerPrefs.SetInt(Prefijo + "congelamiento", ajustes.SegundosCongelamiento);
            PlayerPrefs.SetInt(Prefijo + "jugadores", ajustes.MaxJugadores);
            PlayerPrefs.Save();
        }
    }
}
