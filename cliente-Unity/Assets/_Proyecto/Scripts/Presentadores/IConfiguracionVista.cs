using System;

namespace Reactor.Cliente.Presentadores
{
    public enum CampoAjuste
    {
        Valor,           // duración o puntos, según el modo
        Congelamiento,
        Jugadores,
    }

    /// <summary>Lo que el presentador puede pedirle a la escena de configuración (patrón MVP).</summary>
    public interface IConfiguracionVista
    {
        /// <summary>El jugador pulsó - (direccion = -1) o + (direccion = 1) de un campo.</summary>
        event Action<CampoAjuste, int> AjusteCambiado;

        /// <summary>El jugador eligió cómo termina la partida.</summary>
        event Action<Servicios.ModoFin> ModoElegido;

        event Action CrearPulsado;

        /// <summary>El jugador quiere entrar a una partida que ya existe.</summary>
        event Action UnirsePulsado;

        void MostrarModo(Servicios.ModoFin modo);
        void MostrarValor(string etiqueta, string valor);
        void MostrarCongelamiento(string valor);
        void MostrarJugadores(string valor);
        void BloquearCrear(bool bloqueado);
        void MostrarAviso(string texto);
    }
}
