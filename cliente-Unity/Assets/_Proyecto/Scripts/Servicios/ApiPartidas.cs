using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Cliente REST real de las partidas. Habla con /api/partidas del servidor.</summary>
    public sealed class ApiPartidas : IApiPartidas
    {
        readonly string servidor;
        readonly SessionStore sesion;

        public ApiPartidas(string servidor, SessionStore sesion)
        {
            this.servidor = servidor.TrimEnd('/');
            this.sesion = sesion;
        }

        public Task<string> CrearPartidaAsync(string usuarioId, AjustesPartida ajustes)
        {
            // Los nombres coinciden con CrearPartidaSolicitud del servidor (camelCase).
            var cuerpo = JsonConvert.SerializeObject(new
            {
                usuarioId,
                maxJugadores = ajustes.MaxJugadores,
                modoFin = ajustes.Modo.ToString(),
                duracionSegundos = ajustes.DuracionSegundos,
                puntosObjetivo = ajustes.PuntosObjetivo,
                segundosCongelamiento = ajustes.SegundosCongelamiento,
            });

            var solicitud = new UnityWebRequest(servidor + "/api/partidas", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(cuerpo)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            solicitud.SetRequestHeader("Content-Type", "application/json");

            return Enviar(solicitud, texto =>
            {
                var id = JObject.Parse(texto)["id"]?.ToString();
                if (string.IsNullOrEmpty(id)) throw new ApiException("El servidor no devolvió el id de la partida.");
                return id;
            });
        }

        public Task<IReadOnlyList<PartidaAbierta>> ListarAbiertasAsync()
        {
            var solicitud = new UnityWebRequest(servidor + "/api/partidas", UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };

            return Enviar<IReadOnlyList<PartidaAbierta>>(solicitud, texto =>
            {
                var lista = new List<PartidaAbierta>();
                foreach (var item in JArray.Parse(texto))
                {
                    lista.Add(new PartidaAbierta
                    {
                        Id = item["id"]?.ToString(),
                        Anfitrion = item["anfitrion"]?.ToString(),
                        Jugadores = item["jugadores"]?.Value<int>() ?? 0,
                        MaxJugadores = item["maxJugadores"]?.Value<int>() ?? 0,
                    });
                }
                return lista;
            });
        }

        /// <summary>Manda la solicitud y entrega el resultado en el hilo principal de Unity.</summary>
        Task<T> Enviar<T>(UnityWebRequest solicitud, Func<string, T> leer)
        {
            if (sesion.EstaAutenticado) solicitud.SetRequestHeader("Authorization", "Bearer " + sesion.Token);

            var fuente = new TaskCompletionSource<T>();
            var operacion = solicitud.SendWebRequest();
            operacion.completed += _ =>
            {
                try
                {
                    fuente.SetResult(leer(Validar(solicitud)));
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
                throw new ApiException("No se pudo conectar con el servidor. Revisa la IP en config.json.");

            if (solicitud.result != UnityWebRequest.Result.Success)
                throw new ApiException(MensajeDeError(texto) ?? $"El servidor respondió con el error {solicitud.responseCode}.");

            return texto;
        }

        /// <summary>El servidor responde los errores como {"error": "mensaje"}.</summary>
        static string MensajeDeError(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            try
            {
                return JObject.Parse(texto)["error"]?.ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
