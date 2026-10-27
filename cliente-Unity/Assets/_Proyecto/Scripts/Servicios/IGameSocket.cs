using System;
using Reactor.Cliente.Contratos;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Canal de tiempo real con el servidor (US-13). El cliente nunca decide nada:
    /// solo recibe y muestra lo que el servidor autoritativo confirma.
    /// Implementaciones intercambiables (Liskov): GameSocket (real) y SocketFalso (desarrollo).
    /// </summary>
    public interface IGameSocket
    {
        event Action Conectado;
        event Action<EstadoPartidaMsg> EstadoRecibido;
        event Action<JugadorMovidoMsg> JugadorMovido;
        event Action<JugadorActuoMsg> JugadorActuo;
        event Action<PuntajeMsg> PuntajeCambiado;
        event Action<TickMsg> Tick;
        event Action<TiempoExtraMsg> TiempoExtra;
        event Action<PartidaFinalizadaMsg> PartidaFinalizada;
        event Action<string> Error;

        void Conectar(string partidaId);
        void Enviar(string tipo, object datos);

        /// <summary>Se llama una vez por frame desde el CompositionRoot.</summary>
        void Despachar();

        void Cerrar();
    }
}
