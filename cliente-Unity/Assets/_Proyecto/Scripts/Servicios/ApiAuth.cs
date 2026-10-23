using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Cliente REST real de cuentas. Habla con /api/auth/* y /api/avatares del servidor.</summary>
    public sealed class ApiAuth : IApiAuth
    {
        readonly string servidor;
        readonly SessionStore sesion;

        public ApiAuth(string servidor, SessionStore sesion)
        {
            this.servidor = servidor.TrimEnd('/');
            this.sesion = sesion;
        }

        public Task RegistrarAsync(string usuario, string correo, string clave, string confirmar) =>
            Enviar("POST", "/api/auth/register",
                new { username = usuario, email = correo, password = clave, confirmPassword = confirmar }, _ => true);

        public Task VerificarCorreoAsync(string codigo) =>
            Enviar("POST", "/api/auth/verify-email", new { token = codigo }, _ => true);

        public Task<SesionIniciada> LoginAsync(string usuarioOCorreo, string clave) =>
            Enviar("POST", "/api/auth/login", new { usernameOrEmail = usuarioOCorreo, password = clave }, texto =>
            {
                var json = JObject.Parse(texto);
                var usuario = json["user"];
                var token = json["accessToken"]?.ToString();
                if (string.IsNullOrEmpty(token) || usuario == null)
                    throw new ApiAuthException("RESPUESTA_INVALIDA", "El servidor no devolvió la sesión.");

                return new SesionIniciada
                {
                    Token = token,
                    UsuarioId = usuario["id"]?.ToString(),
                    Nombre = usuario["username"]?.ToString(),
                    AvatarId = NullSiVacio(usuario["avatarId"]),
                };
            });

        public Task SolicitarRecuperacionAsync(string correo) =>
            Enviar("POST", "/api/auth/forgot-password", new { email = correo }, _ => true);

        public Task RestablecerAsync(string codigo, string clave, string confirmar) =>
            Enviar("POST", "/api/auth/reset-password",
                new { token = codigo, newPassword = clave, confirmPassword = confirmar }, _ => true);

        public Task<IReadOnlyList<AvatarInfo>> ListarAvataresAsync() =>
            Enviar<IReadOnlyList<AvatarInfo>>("GET", "/api/avatares", null, texto =>
            {
                var lista = new List<AvatarInfo>();
                foreach (var item in JArray.Parse(texto))
                {
                    lista.Add(new AvatarInfo
                    {
                        Id = item["id"]?.ToString(),
                        Nombre = item["nombre"]?.ToString(),
                        Color = item["color"]?.ToString(),
                        Propio = item["propio"]?.Value<bool>() ?? false,
                    });
                }
                return lista;
            });

        public Task<PerfilUsuario> PerfilAsync() =>
            Enviar("GET", "/api/auth/profile", null, texto =>
            {
                var json = JObject.Parse(texto);
                return new PerfilUsuario
                {
                    Id = json["id"]?.ToString(),
                    Nombre = json["username"]?.ToString(),
                    Correo = json["email"]?.ToString(),
                    AvatarId = NullSiVacio(json["avatarId"]),
                };
            });

        public Task ElegirAvatarAsync(string avatarId) =>
            Enviar("PUT", "/api/auth/profile/avatar", new { avatarId }, _ => true);

        public Task<string> SubirAvatarAsync(byte[] png) =>
            Enviar("POST", "/api/auth/profile/avatar/upload", null, texto => NullSiVacio(JObject.Parse(texto)["avatarId"]), png, "image/png");

        static string NullSiVacio(JToken token) =>
            token == null || token.Type == JTokenType.Null || string.IsNullOrEmpty(token.ToString()) ? null : token.ToString();

        Task<T> Enviar<T>(string verbo, string ruta, object cuerpo, Func<string, T> leer, byte[] crudo = null, string tipoCrudo = null)
        {
            var solicitud = new UnityWebRequest(servidor + ruta, verbo)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            if (cuerpo != null)
            {
                solicitud.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cuerpo)));
                solicitud.SetRequestHeader("Content-Type", "application/json");
            }
            if (crudo != null)
            {
                solicitud.uploadHandler = new UploadHandlerRaw(crudo);
                solicitud.SetRequestHeader("Content-Type", tipoCrudo ?? "application/octet-stream");
            }
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
