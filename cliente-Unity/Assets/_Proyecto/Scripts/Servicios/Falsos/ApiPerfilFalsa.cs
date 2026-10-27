using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// Perfil de mentira para probar la pantalla de Inicio sin servidor: el jugador local ("Ana") tiene 5 partidas,
    /// 3 victorias y 96 puntos, así que se ven logros obtenidos y logros a medias.
    /// </summary>
    public sealed class ApiPerfilFalsa : IApiPerfil
    {
        readonly string idLocal;
        readonly string nombreLocal;

        public ApiPerfilFalsa(string idLocal, string nombreLocal = "Ana")
        {
            this.idLocal = idLocal;
            this.nombreLocal = nombreLocal;
        }

        public Task<PerfilJugador> ObtenerPerfilAsync() => Task.FromResult(new PerfilJugador
        {
            Id = idLocal,
            Nombre = nombreLocal,
            Correo = nombreLocal.ToLowerInvariant() + "@correo.com",
            CorreoVerificado = true,
            AvatarId = "cc2ec6b9-97f9-41ca-8985-2fc3280f7e62",
            Estadisticas = new EstadisticasJugador
            {
                PartidasJugadas = 5, Victorias = 3, Derrotas = 2, PuntosTotales = 96, MejorPuntaje = 31, PromedioPuntos = 19.2,
            },
            Logros = new List<LogroJugador>
            {
                new LogroJugador { Id = "primera-partida", Nombre = "Primera partida", Descripcion = "Juega tu primera partida.", Obtenido = true, Progreso = 1, Meta = 1 },
                new LogroJugador { Id = "primera-victoria", Nombre = "Primera victoria", Descripcion = "Gana una partida.", Obtenido = true, Progreso = 1, Meta = 1 },
                new LogroJugador { Id = "cinco-victorias", Nombre = "Cinco victorias", Descripcion = "Gana 5 partidas.", Obtenido = false, Progreso = 3, Meta = 5 },
                new LogroJugador { Id = "cien-puntos", Nombre = "100 puntos", Descripcion = "Acumula 100 puntos entre todas tus partidas.", Obtenido = false, Progreso = 96, Meta = 100 },
            },
        });

        public Task<IReadOnlyList<PartidaHistorial>> ObtenerHistorialAsync(int limite = 10)
        {
            var ahora = DateTime.UtcNow;
            var lista = new List<PartidaHistorial>
            {
                Partida(ahora.AddHours(-2), "Victoria", 31, 12, "Beto", 95),
                Partida(ahora.AddHours(-26), "Derrota", 9, 20, "Carla", 180),
                Partida(ahora.AddHours(-30), "Victoria", 22, 8, "Beto", 120),
                Partida(ahora.AddHours(-50), "Victoria", 25, 25, "Dani", 300),
                Partida(ahora.AddHours(-75), "Derrota", 9, 14, "Beto", 60),
            };
            return Task.FromResult<IReadOnlyList<PartidaHistorial>>(lista.GetRange(0, Math.Min(limite, lista.Count)));
        }

        public Task<IReadOnlyList<EntradaRankingJugador>> ObtenerRankingAsync(string criterio, int limite = 8)
        {
            var porPuntos = string.Equals(criterio, "puntos", StringComparison.OrdinalIgnoreCase);
            var lista = porPuntos
                ? new List<EntradaRankingJugador>
                {
                    Entrada(1, "Beto", 2, 4, 120), Entrada(2, nombreLocal, 3, 5, 96, idLocal),
                    Entrada(3, "Carla", 1, 2, 51), Entrada(4, "Dani", 0, 1, 25),
                }
                : new List<EntradaRankingJugador>
                {
                    Entrada(1, nombreLocal, 3, 5, 96, idLocal), Entrada(2, "Beto", 2, 4, 120),
                    Entrada(3, "Carla", 1, 2, 51), Entrada(4, "Dani", 0, 1, 25),
                };
            return Task.FromResult<IReadOnlyList<EntradaRankingJugador>>(lista.GetRange(0, Math.Min(limite, lista.Count)));
        }

        PartidaHistorial Partida(DateTime fin, string resultado, int mio, int rival, string nombreRival, int segundos) => new PartidaHistorial
        {
            PartidaId = Guid.NewGuid().ToString(),
            Fin = fin,
            DuracionSegundos = segundos,
            ModoFin = "Tiempo",
            Resultado = resultado,
            MiPuntaje = mio,
            Jugadores = new List<JugadorHistorial>
            {
                new JugadorHistorial { UsuarioId = idLocal, Nombre = nombreLocal, Puntaje = mio, EsGanador = resultado == "Victoria" },
                new JugadorHistorial { UsuarioId = Guid.NewGuid().ToString(), Nombre = nombreRival, Puntaje = rival, EsGanador = resultado != "Victoria" },
            },
        };

        static EntradaRankingJugador Entrada(int posicion, string nombre, int victorias, int partidas, int puntos, string id = null) =>
            new EntradaRankingJugador
            {
                Posicion = posicion,
                UsuarioId = id ?? Guid.NewGuid().ToString(),
                Nombre = nombre,
                Victorias = victorias,
                Partidas = partidas,
                PuntosTotales = puntos,
            };
    }
}
