using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Cliente REST real del perfil. Habla con /api/perfil, /api/perfil/historial y /api/ranking.</summary>
    public sealed class ApiPerfil : IApiPerfil
    {
        readonly string servidor;
        readonly SessionStore sesion;

        public ApiPerfil(string servidor, SessionStore sesion)
        {
            this.servidor = servidor.TrimEnd('/');
            this.sesion = sesion;
        }

        public Task<PerfilJugador> ObtenerPerfilAsync() =>
            Obtener<PerfilJugador>("/api/perfil");

        public async Task<IReadOnlyList<PartidaHistorial>> ObtenerHistorialAsync(int limite = 10) =>
            await Obtener<List<PartidaHistorial>>("/api/perfil/historial?limite=" + limite);

        public async Task<IReadOnlyList<EntradaRankingJugador>> ObtenerRankingAsync(string criterio, int limite = 8) =>
            await Obtener<List<EntradaRankingJugador>>(
                "/api/ranking?criterio=" + UnityWebRequest.EscapeURL(criterio ?? "victorias") + "&limite=" + limite);

        Task<T> Obtener<T>(string ruta)
        {
            var solicitud = UnityWebRequest.Get(servidor + ruta);
            solicitud.timeout = 10;
            if (sesion != null && sesion.EstaAutenticado)
                solicitud.SetRequestHeader("Authorization", "Bearer " + sesion.Token);

            var fuente = new TaskCompletionSource<T>();
            UnityWebRequestAsyncOperation operacion;
            try
            {
                operacion = solicitud.SendWebRequest();
            }
            catch (Exception e)
            {
                solicitud.Dispose();
                fuente.SetException(new ApiAuthException("SIN_CONEXION", "No se pudo enviar la solicitud: " + e.Message));
                return fuente.Task;
            }

            operacion.completed += _ =>
            {
                try
                {
                    fuente.SetResult(JsonConvert.DeserializeObject<T>(Validar(solicitud)));
                }
                catch (Exception e)
                {
                    fuente.SetException(e);
                }
                finally
                {
                    solicitud.Dispose();
                }
            };
            return fuente.Task;
        }

        static string Validar(UnityWebRequest solicitud)
        {
            var texto = solicitud.downloadHandler?.text;

            if (solicitud.result == UnityWebRequest.Result.ConnectionError)
                throw new ApiAuthException("SIN_CONEXION", "No se pudo conectar con el servidor. Revisa la IP en config.json.");

            if (solicitud.result != UnityWebRequest.Result.Success)
            {
                if (solicitud.responseCode == 401)
                    throw new ApiAuthException("NO_AUTORIZADO", "Tu sesión no es válida. Inicia sesión de nuevo.");

                var (codigo, mensaje) = LeerError(texto);
                throw new ApiAuthException(codigo, mensaje ?? $"El servidor respondió con el error {solicitud.responseCode}.");
            }

            return texto;
        }

        /// <summary>El servidor responde los errores como {"error","codigo","mensaje"}.</summary>
        static (string codigo, string mensaje) LeerError(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return ("", null);
            try
            {
                var json = JObject.Parse(texto);
                return (json["codigo"]?.ToString() ?? "", json["mensaje"]?.ToString() ?? json["error"]?.ToString());
            }
            catch (JsonException)
            {
                return ("", null);
            }
        }
    }
}
