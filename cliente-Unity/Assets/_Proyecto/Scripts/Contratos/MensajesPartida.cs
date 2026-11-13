using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reactor.Cliente.Contratos
{
    // Copia de los mensajes acordados en Docs/contratos-websocket.md.
    // Los nombres de C# están en español; en el cable (JSON) van en inglés, como define el contrato
    // con el servidor. Cada propiedad lleva [JsonProperty] con su nombre exacto en el cable.
    // OJO: en modo POINTS el servidor manda "timeLeft": null. Con NullValueHandling.Ignore el valor
    // null se omite y la propiedad queda en 0 (en ese modo Unity muestra timeElapsed).
    // Cuando la biblioteca Contratos compile para netstandard2.1, este archivo se reemplaza por la DLL compartida.

    public static class TiposMensaje
    {
        // Cliente -> servidor
        public const string Unirse = "match:join";
        public const string Iniciar = "match:start";

        // Servidor -> cliente
        public const string Estado = "match:state";
        public const string JugadorMovido = "player:moved";
        public const string JugadorActuo = "player:acted"; // ya no lo envia el servidor desde el Sprint 3 (ACTION ahora es Extraer)
        public const string JugadorExtrajo = "player:extracted";
        public const string JugadorRechazado = "player:rejected";
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

    /// <summary>Modo de fin de partida (endMode: TIME | POINTS).</summary>
    public static class ModosFin
    {
        public const string Tiempo = "TIME";
        public const string Puntos = "POINTS";
    }

    /// <summary>Estado del reactor (reactorState / state en el cable).</summary>
    public static class EstadosReactor
    {
        public const string Disponible = "AVAILABLE";
        public const string Vacio = "EMPTY";
        public const string Desactivado = "DISABLED";
    }

    /// <summary>Caracteres de las filas del escenario.</summary>
    public static class Celdas
    {
        public const char Muro = 'W';
        public const char Piso = '.';
        public const char Inicio = 'S';
        public const char Reactor = 'R';
    }

    /// <summary>Colores por orden de llegada, para cuando el servidor no manda color.</summary>
    public static class PaletaJugadores
    {
        static readonly string[] Colores = { "#E74C3C", "#3498DB", "#2ECC71", "#F1C40F" };

        public static string PorOrden(int indice) => Colores[indice % Colores.Length];
    }

    // ---------- Cliente -> servidor ----------

    public class UnirseMsg
    {
        [JsonProperty("matchId")] public string PartidaId { get; set; }
    }

    // ---------- Servidor -> cliente ----------

    public class EstadoPartidaMsg
    {
        [JsonProperty("matchId")] public string PartidaId { get; set; }
        [JsonProperty("hostId")] public string AnfitrionId { get; set; }
        [JsonProperty("status")] public string Estado { get; set; }
        [JsonProperty("endMode")] public string ModoFin { get; set; }
        [JsonProperty("endValue")] public int ValorFin { get; set; }
        [JsonProperty("timeLeft", NullValueHandling = NullValueHandling.Ignore)] public int SegundosRestantes { get; set; }
        [JsonProperty("timeElapsed")] public int SegundosTranscurridos { get; set; }
        [JsonProperty("scenario")] public EscenarioMsg Escenario { get; set; }
        [JsonProperty("reactor")] public ReactorMsg Reactor { get; set; } // null si el servidor aun no lo manda
        [JsonProperty("players")] public List<JugadorMsg> Jugadores { get; set; } = new List<JugadorMsg>();

        /// <summary>Si el servidor no envía color, se asigna uno por orden de llegada.</summary>
        public void AsignarColoresFaltantes()
        {
            if (Jugadores == null) return;
            for (var i = 0; i < Jugadores.Count; i++)
                if (Jugadores[i] != null && string.IsNullOrEmpty(Jugadores[i].Color))
                    Jugadores[i].Color = PaletaJugadores.PorOrden(i);
        }
    }

    public class EscenarioMsg
    {
        [JsonProperty("width")] public int Ancho { get; set; }
        [JsonProperty("height")] public int Alto { get; set; }
        [JsonProperty("rows")] public List<string> Filas { get; set; } = new List<string>();
    }

    public class JugadorMsg
    {
        [JsonProperty("userId")] public string Id { get; set; }
        [JsonProperty("username")] public string Nombre { get; set; }
        [JsonProperty("avatarId")] public string AvatarId { get; set; }
        [JsonProperty("avatarUrl")] public string AvatarUrl { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("score")] public int Puntaje { get; set; }
    }

    public class JugadorMovidoMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("score")] public int Puntaje { get; set; }
    }

    public class JugadorActuoMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("message")] public string Mensaje { get; set; }
    }

    public class ReactorMsg
    {
        [JsonProperty("energy")] public int Energia { get; set; }
        [JsonProperty("capacity")] public int Capacidad { get; set; }
        [JsonProperty("state")] public string Estado { get; set; }
    }

    /// <summary>player:extracted: a toda la sala cuando una extraccion es valida.</summary>
    public class JugadorExtrajoMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("points")] public int Puntos { get; set; }
        [JsonProperty("score")] public int Puntaje { get; set; }
        [JsonProperty("playerEnergy")] public int EnergiaJugador { get; set; }
        [JsonProperty("reactorEnergy")] public int EnergiaReactor { get; set; }
        [JsonProperty("reactorState")] public string EstadoReactor { get; set; }
        [JsonProperty("eventId")] public string IdEvento { get; set; }
    }

    /// <summary>player:rejected: solo al jugador que causo el rechazo.</summary>
    public class JugadorRechazadoMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("code")] public string Codigo { get; set; }
        [JsonProperty("message")] public string Mensaje { get; set; }
    }

    /// <summary>Textos cortos por codigo de rechazo, para mostrar al jugador.</summary>
    public static class TextosRechazo
    {
        public static string Para(string codigo)
        {
            switch (codigo)
            {
                case "LEJOS_DEL_REACTOR": return "Acércate al reactor";
                case "JUGADOR_CONGELADO": return "Estás congelado";
                case "ENFRIAMIENTO": return "Espera un momento";
                case "REACTOR_VACIO": return "El reactor está vacío";
                case "REACTOR_DESACTIVADO": return "El reactor está en sobrecarga";
                case "PARTIDA_NO_ACTIVA": return "La partida no está en curso";
                case "JUGADOR_ELIMINADO": return "Estás fuera de la partida";
                default: return "Acción no válida";
            }
        }
    }

    public class PuntajeMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("score")] public int Puntaje { get; set; }
    }

    public class TickMsg
    {
        [JsonProperty("timeLeft", NullValueHandling = NullValueHandling.Ignore)] public int SegundosRestantes { get; set; }
        [JsonProperty("timeElapsed")] public int SegundosTranscurridos { get; set; }
    }

    public class TiempoExtraMsg
    {
        [JsonProperty("extraMinutes")] public int MinutosExtra { get; set; }
        [JsonProperty("timeLeft", NullValueHandling = NullValueHandling.Ignore)] public int SegundosRestantes { get; set; }
    }

    public class PartidaFinalizadaMsg
    {
        [JsonProperty("winnerId")] public string GanadorId { get; set; }
        [JsonProperty("scores")] public List<PuntajeFinalMsg> Puntajes { get; set; } = new List<PuntajeFinalMsg>();
    }

    public class PuntajeFinalMsg
    {
        [JsonProperty("userId")] public string JugadorId { get; set; }
        [JsonProperty("username")] public string Nombre { get; set; }
        [JsonProperty("score")] public int Puntaje { get; set; }
    }

    public class ErrorMsg
    {
        [JsonProperty("message")] public string Mensaje { get; set; }
    }
}
