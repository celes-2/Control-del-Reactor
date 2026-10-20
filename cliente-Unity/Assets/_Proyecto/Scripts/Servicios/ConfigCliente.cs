using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Lee StreamingAssets/config.json para saber dónde está el servidor.
    /// Así se cambia la IP en la demostración sin volver a compilar.
    ///
    /// Ejemplo:  { "servidor": "http://192.168.1.50:8080" }
    ///
    /// Opcional, SOLO mientras no exista el login con JWT: "usuarioId" y "nombreUsuario" identifican a
    /// este equipo ante el servidor real (el id debe ser el de un usuario que ya exista, con correo
    /// verificado y avatar elegido). Cada computadora usa un usuarioId distinto.
    /// </summary>
    public sealed class ConfigCliente
    {
        const string ServidorPorDefecto = "http://localhost:8080";

        public string Servidor { get; }

        /// <summary>URL del WebSocket de partidas: ws://IP:8080/ws/partida (el token o el usuario se agregan al conectar).</summary>
        public string UrlWebSocket { get; }

        public string UsuarioIdProvisional { get; }
        public string NombreProvisional { get; }

        ConfigCliente(string servidor, string usuarioId, string nombre)
        {
            Servidor = servidor;
            UrlWebSocket = AWebSocket(servidor) + "/ws/partida";
            UsuarioIdProvisional = usuarioId;
            NombreProvisional = nombre;
        }

        public static ConfigCliente Cargar()
        {
            var servidor = ServidorPorDefecto;
            string usuarioId = null;
            string nombre = null;
            var ruta = Path.Combine(Application.streamingAssetsPath, "config.json");
            try
            {
                if (File.Exists(ruta))
                {
                    var archivo = JsonConvert.DeserializeObject<ArchivoConfig>(File.ReadAllText(ruta));
                    if (!string.IsNullOrWhiteSpace(archivo?.servidor))
                        servidor = archivo.servidor.Trim().TrimEnd('/');
                    if (!string.IsNullOrWhiteSpace(archivo?.usuarioId))
                        usuarioId = archivo.usuarioId.Trim();
                    if (!string.IsNullOrWhiteSpace(archivo?.nombreUsuario))
                        nombre = archivo.nombreUsuario.Trim();
                }
                else
                {
                    Debug.LogWarning($"No existe {ruta}; se usa {ServidorPorDefecto}.");
                }
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                Debug.LogWarning($"No se pudo leer config.json ({e.Message}); se usa {ServidorPorDefecto}.");
            }
            return new ConfigCliente(servidor, usuarioId, nombre);
        }

        static string AWebSocket(string servidor)
        {
            if (servidor.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "wss://" + servidor.Substring(8);
            if (servidor.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "ws://" + servidor.Substring(7);
            return servidor;
        }

        [Serializable]
        sealed class ArchivoConfig
        {
            public string servidor;
            public string usuarioId;
            public string nombreUsuario;
        }
    }
}
