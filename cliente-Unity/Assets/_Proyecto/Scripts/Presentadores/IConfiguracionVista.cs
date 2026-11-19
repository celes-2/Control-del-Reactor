using System;

namespace Reactor.Cliente.Presentadores
{
    public enum CampoAjuste
    {
        Valor,           // duración o puntos, según el modo
        Congelamiento,
        Jugadores,
        Escenario,       // pasa al escenario anterior o siguiente
        Guardianes,      // de 2 a 4
    }

    /// <summary>Lo que el presentador puede pedirle a la escena de configuración (patrón MVP).</summary>
    public interface IConfiguracionVista
    {
        /// <summary>El jugador pulsó - (direccion = -1) o + (direccion = 1) de un campo.</summary>
        event Action<CampoAjuste, int> AjusteCambiado;

        /// <summary>El jugador eligió cómo termina la partida.</summary>
        event Action<Servicios.ModoFin> ModoElegido;

        event Action CrearPulsado;

        /// <summary>El jugador tocó un escenario en la pantalla de elegir escenario (llega su id).</summary>
        event Action<string> EscenarioElegido;

        /// <summary>El jugador quiere volver a la pantalla de elegir escenario.</summary>
        event Action VolverAEscenariosPulsado;

        /// <summary>El jugador quiere entrar a una partida que ya existe.</summary>
        event Action UnirsePulsado;

        void MostrarModo(Servicios.ModoFin modo);
        void MostrarValor(string etiqueta, string valor);
        void MostrarCongelamiento(string valor);
        void MostrarJugadores(string valor);
        void MostrarEscenario(Servicios.EscenarioInfo escenario);
        void MostrarGuardianes(string valor);
        /// <summary>true = pantalla solo para elegir escenario; false = ajustes de la partida.</summary>
        void MostrarPaso(bool eligiendoEscenario);

        /// <summary>Dibuja una tarjeta con vista previa por escenario y resalta el actual.</summary>
        void MostrarSelectorEscenarios(System.Collections.Generic.IReadOnlyList<Servicios.EscenarioInfo> escenarios, string idActual);

        void BloquearCrear(bool bloqueado);
        void MostrarAviso(string texto);
    }
}
