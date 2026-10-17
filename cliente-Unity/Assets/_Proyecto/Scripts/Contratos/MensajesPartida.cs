using System.Collections.Generic;

namespace Reactor.Cliente.Contratos
{
    // Copia provisional de los mensajes acordados en docs/contratos-websocket.md.
    // Cuando la biblioteca Contratos compile para netstandard2.1, este archivo
    // se reemplaza por la DLL compartida con el servidor.

    public static class TiposMensaje
    {
        // Cliente -> servidor
        public const string Unirse = "match:join";
        public const string Iniciar = "match:start";

        // Servidor -> cliente
        public const string Estado = "match:state";
        public const string JugadorMovido = "player:moved";
        public const string JugadorActuo = "player:acted";
        public const string PuntajeCambiado = "score:changed";
        public const string Tick = "match:tick";
        public const string TiempoExtra = "match:overtime";
        public const string PartidaFinalizada = "match:finished";
        public const string Error = "error";
    }

    /// <summary>Botones del dispositivo, iguales a los que publica la Pico W por MQTT.</summary>
    public static class Botones
    {
        public const string Arriba = "UP";
        public const string Abajo = "DOWN";
        public const string Izquierda = "LEFT";
        public const string Derecha = "RIGHT";
        public const string Accion = "ACTION";
    }

    public static class EstadosPartida
    {
        public const string Espera = "Espera";
        public const string EnCurso = "EnCurso";
        public const string Finalizada = "Finalizada";
    }

    /// <summary>Modo de fin de partida (config.json: endMode TIME | POINTS).</summary>
    public static class ModosFin
    {
        public const string Tiempo = "TIME";
        public const string Puntos = "POINTS";
    }

    /// <summary>Caracteres de las filas del escenario (scenarios.json).</summary>
    public static class Celdas
    {
        public const char Muro = 'W';
        public const char Piso = '.';
        public const char Inicio = 'S';
        public const char Reactor = 'R';
    }

    public class EstadoPartidaMsg
    {
        public string PartidaId { get; set; }
        public string Estado { get; set; }
        public string AnfitrionId { get; set; }
        public string ModoFin { get; set; }
        public int ValorFin { get; set; }
        public int SegundosRestantes { get; set; }
        public int SegundosTranscurridos { get; set; }
        public EscenarioMsg Escenario { get; set; }
        public List<JugadorMsg> Jugadores { get; set; } = new List<JugadorMsg>();
    }

    public class EscenarioMsg
    {
        public int Ancho { get; set; }
        public int Alto { get; set; }
        public List<string> Filas { get; set; } = new List<string>();
    }

    public class JugadorMsg
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string AvatarUrl { get; set; }
        public string Color { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Puntaje { get; set; }
    }

    public class JugadorMovidoMsg
    {
        public string JugadorId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Puntaje { get; set; }
    }

    public class JugadorActuoMsg
    {
        public string JugadorId { get; set; }
        public string Mensaje { get; set; }
    }

    public class PuntajeMsg
    {
        public string JugadorId { get; set; }
        public int Puntaje { get; set; }
    }

    public class TickMsg
    {
        public int SegundosRestantes { get; set; }
        public int SegundosTranscurridos { get; set; }
    }

    public class TiempoExtraMsg
    {
        public int MinutosExtra { get; set; }
        public int SegundosRestantes { get; set; }
    }

    public class PartidaFinalizadaMsg
    {
        public string GanadorId { get; set; }
        public List<PuntajeFinalMsg> Puntajes { get; set; } = new List<PuntajeFinalMsg>();
    }

    public class PuntajeFinalMsg
    {
        public string JugadorId { get; set; }
        public string Nombre { get; set; }
        public int Puntaje { get; set; }
    }
}
