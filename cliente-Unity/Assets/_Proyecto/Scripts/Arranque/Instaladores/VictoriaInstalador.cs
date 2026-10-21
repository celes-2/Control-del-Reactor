using System.Collections.Generic;
using Reactor.Cliente.Contratos;
using Reactor.Cliente.Presentadores;
using Reactor.Cliente.Servicios;
using Reactor.Cliente.Victoria;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reactor.Cliente.Arranque
{
    /// <summary>
    /// Arma la escena Victoria: toma el resultado guardado en la sesión y crea el presentador.
    /// Si se da Play directamente en esta escena, primero pasa por Arranque.
    /// </summary>
    [RequireComponent(typeof(VictoriaVista))]
    public sealed class VictoriaInstalador : MonoBehaviour
    {
        [Tooltip("Escena a la que se vuelve con el botón. Cuando exista la Sala de espera, cambiarla aquí.")]
        [SerializeField] string escenaAlVolver = Escenas.Arena;

        [Tooltip("SOLO desarrollo: si no hay un resultado real, muestra uno de ejemplo para ver la pantalla.")]
        [SerializeField] bool usarResultadoDeEjemplo;

        VictoriaPresentador presentador;

        void Start()
        {
            var raiz = FindAnyObjectByType<CompositionRoot>();
            if (raiz == null)
            {
                SceneManager.LoadScene(Escenas.Arranque);
                return;
            }

            var resultado = raiz.Sesion.UltimoResultado;
            if (resultado == null && usarResultadoDeEjemplo) resultado = Ejemplo();

            presentador = new VictoriaPresentador(
                GetComponent<VictoriaVista>(), resultado, () => SceneManager.LoadScene(escenaAlVolver));
            presentador.Iniciar();
        }

        void OnDestroy() => presentador?.Dispose();

        static ResultadoPartida Ejemplo()
        {
            var jugadores = new List<JugadorMsg>
            {
                new JugadorMsg { Id = "jugador-1", Nombre = "Ana", Color = "#E74C3C" },
                new JugadorMsg { Id = "jugador-2", Nombre = "Beto", Color = "#3498DB" },
            };
            var mensaje = new PartidaFinalizadaMsg
            {
                GanadorId = "jugador-1",
                Puntajes = new List<PuntajeFinalMsg>
                {
                    new PuntajeFinalMsg { JugadorId = "jugador-2", Nombre = "Beto", Puntaje = 9 },
                    new PuntajeFinalMsg { JugadorId = "jugador-1", Nombre = "Ana", Puntaje = 14 },
                },
            };
            return ResultadoPartida.Crear(mensaje, jugadores);
        }
    }
}
