using System;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Decide qué se muestra en la pantalla de victoria (US-17). Es C# puro, así que se prueba
    /// con una vista falsa. No calcula el ganador: lo recibe del servidor ya decidido.
    /// </summary>
    public sealed class VictoriaPresentador : IDisposable
    {
        readonly IVictoriaVista vista;
        readonly ResultadoPartida resultado;
        readonly Action alVolver;

        public VictoriaPresentador(IVictoriaVista vista, ResultadoPartida resultado, Action alVolver)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.alVolver = alVolver ?? throw new ArgumentNullException(nameof(alVolver));
            this.resultado = resultado;

            vista.VolverPulsado += AlPulsarVolver;
        }

        public void Iniciar()
        {
            if (resultado == null || resultado.Jugadores.Count == 0)
            {
                vista.MostrarSinResultado("No hay un resultado de partida para mostrar.");
                return;
            }

            var ganador = resultado.Ganador;
            if (ganador != null)
            {
                var nombre = string.IsNullOrWhiteSpace(ganador.Nombre) ? "Un jugador" : ganador.Nombre;
                vista.MostrarGanador(ganador, "¡FELICIDADES!", $"{nombre} gana con {ganador.Puntaje} puntos");
                vista.MostrarPuntajes(resultado.Jugadores);
                vista.Celebrar();
            }
            else
            {
                vista.MostrarSinGanador("FIN DE LA PARTIDA", "No hubo ganador");
                vista.MostrarPuntajes(resultado.Jugadores);
            }
        }

        public void Dispose() => vista.VolverPulsado -= AlPulsarVolver;

        void AlPulsarVolver() => alVolver();
    }
}
