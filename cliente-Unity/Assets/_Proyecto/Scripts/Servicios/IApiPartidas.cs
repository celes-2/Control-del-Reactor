using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Error que el servidor devolvió con un mensaje para mostrar al usuario.</summary>
    public sealed class ApiException : Exception
    {
        public ApiException(string mensaje) : base(mensaje) { }
    }

    /// <summary>Lo que Unity necesita del API REST de partidas.</summary>
    public interface IApiPartidas
    {
        /// <summary>Crea la partida con esos ajustes y devuelve su id. Lanza <see cref="ApiException"/> si el servidor la rechaza.</summary>
        Task<string> CrearPartidaAsync(string usuarioId, AjustesPartida ajustes);

        /// <summary>Partidas en espera a las que se puede entrar (GET /api/partidas).</summary>
        Task<IReadOnlyList<PartidaAbierta>> ListarAbiertasAsync();
    }
}
