using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Reactor.Cliente.Servicios
{
    // Modelos de US-04. Los nombres JSON son los de Contratos/Perfil del servidor.

    public sealed class EstadisticasJugador
    {
        [JsonProperty("partidasJugadas")] public int PartidasJugadas { get; set; }
        [JsonProperty("victorias")] public int Victorias { get; set; }
        [JsonProperty("derrotas")] public int Derrotas { get; set; }
        [JsonProperty("puntosTotales")] public int PuntosTotales { get; set; }
        [JsonProperty("mejorPuntaje")] public int MejorPuntaje { get; set; }
        [JsonProperty("promedioPuntos")] public double PromedioPuntos { get; set; }
    }

    public sealed class LogroJugador
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("nombre")] public string Nombre { get; set; }
        [JsonProperty("descripcion")] public string Descripcion { get; set; }
        [JsonProperty("obtenido")] public bool Obtenido { get; set; }
        [JsonProperty("progreso")] public int Progreso { get; set; }
        [JsonProperty("meta")] public int Meta { get; set; }
    }

    public sealed class PerfilJugador
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("username")] public string Nombre { get; set; }
        [JsonProperty("email")] public string Correo { get; set; }
        [JsonProperty("emailVerified")] public bool CorreoVerificado { get; set; }
        [JsonProperty("avatarId")] public string AvatarId { get; set; }
        [JsonProperty("estadisticas")] public EstadisticasJugador Estadisticas { get; set; } = new EstadisticasJugador();
        [JsonProperty("logros")] public List<LogroJugador> Logros { get; set; } = new List<LogroJugador>();
    }

    public sealed class JugadorHistorial
    {
        [JsonProperty("usuarioId")] public string UsuarioId { get; set; }
        [JsonProperty("nombre")] public string Nombre { get; set; }
        [JsonProperty("avatarId")] public string AvatarId { get; set; }
        [JsonProperty("puntaje")] public int Puntaje { get; set; }
        [JsonProperty("esGanador")] public bool EsGanador { get; set; }
    }

    public sealed class PartidaHistorial
    {
        [JsonProperty("partidaId")] public string PartidaId { get; set; }
        [JsonProperty("fin")] public DateTime Fin { get; set; }
        [JsonProperty("duracionSegundos")] public int DuracionSegundos { get; set; }
        [JsonProperty("modoFin")] public string ModoFin { get; set; }
        [JsonProperty("prorrogas")] public int Prorrogas { get; set; }
        /// <summary>"Victoria", "Derrota" o "SinGanador", visto desde el jugador que consulta.</summary>
        [JsonProperty("resultado")] public string Resultado { get; set; }
        [JsonProperty("miPuntaje")] public int MiPuntaje { get; set; }
        [JsonProperty("jugadores")] public List<JugadorHistorial> Jugadores { get; set; } = new List<JugadorHistorial>();
    }

    public sealed class EntradaRankingJugador
    {
        [JsonProperty("posicion")] public int Posicion { get; set; }
        [JsonProperty("usuarioId")] public string UsuarioId { get; set; }
        [JsonProperty("username")] public string Nombre { get; set; }
        [JsonProperty("avatarId")] public string AvatarId { get; set; }
        [JsonProperty("victorias")] public int Victorias { get; set; }
        [JsonProperty("partidas")] public int Partidas { get; set; }
        [JsonProperty("puntosTotales")] public int PuntosTotales { get; set; }
    }

    /// <summary>Lo que Unity necesita del API del perfil (US-04). Todo requiere sesión iniciada.</summary>
    public interface IApiPerfil
    {
        Task<PerfilJugador> ObtenerPerfilAsync();
        Task<IReadOnlyList<PartidaHistorial>> ObtenerHistorialAsync(int limite = 10);

        /// <param name="criterio">"victorias" o "puntos".</param>
        Task<IReadOnlyList<EntradaRankingJugador>> ObtenerRankingAsync(string criterio, int limite = 8);
    }
}
