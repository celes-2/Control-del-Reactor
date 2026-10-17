using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Lee StreamingAssets/config.json para saber dónde está el servidor.
    /// Así se cambia la IP en la demostración sin volver a compilar.
    /// </summary>
    public sealed class ConfigCliente
    {
        const string ServidorPorDefecto = "http://localhost:8080";

        public string Servidor { get; }
        public string UrlWebSocket { get; }

        ConfigCliente(string servidor)
        {
            Servidor = servidor;
            UrlWebSocket = AWebSocket(servidor) + "/ws";
        }

        public static ConfigCliente Cargar()
        {
            var servidor = ServidorPorDefecto;
            var ruta = Path.Combine(Application.streamingAssetsPath, "config.json");
            try
            {
                if (File.Exists(ruta))
                {
                    var archivo = JsonConvert.DeserializeObject<ArchivoConfig>(File.ReadAllText(ruta));
                    if (!string.IsNullOrWhiteSpace(archivo?.servidor))
                        servidor = archivo.servidor.Trim().TrimEnd('/');
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
            return new ConfigCliente(servidor);
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
        }
    }
}
