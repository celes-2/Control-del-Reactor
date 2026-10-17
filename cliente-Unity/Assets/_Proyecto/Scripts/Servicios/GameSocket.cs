using System;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reactor.Cliente.Contratos;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Cliente WebSocket real contra el servidor ASP.NET Core.
    /// Todos los mensajes viajan en un sobre: { "type": "...", "data": { ... } }.
    /// </summary>
    public sealed class GameSocket : IGameSocket
    {
        readonly string url;
        readonly SessionStore sesion;
        WebSocket ws;

        public event Action Conectado;
        public event Action<EstadoPartidaMsg> EstadoRecibido;
        public event Action<JugadorMovidoMsg> JugadorMovido;
        public event Action<JugadorActuoMsg> JugadorActuo;
        public event Action<PuntajeMsg> PuntajeCambiado;
        public event Action<TickMsg> Tick;
        public event Action<TiempoExtraMsg> TiempoExtra;
        public event Action<PartidaFinalizadaMsg> PartidaFinalizada;
        public event Action<string> Error;

        public GameSocket(string url, SessionStore sesion)
        {
            this.url = url ?? throw new ArgumentNullException(nameof(url));
            this.sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        }

        public void Conectar(string partidaId)
        {
            Cerrar();

            var cabeceras = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(sesion.Token))
                cabeceras["Authorization"] = "Bearer " + sesion.Token;

            ws = new WebSocket($"{url}?partidaId={Uri.EscapeDataString(partidaId)}", cabeceras);
            ws.OnOpen += () =>
            {
                Conectado?.Invoke();
                Enviar(TiposMensaje.Unirse, new { partidaId });
            };
            ws.OnMessage += datos => Procesar(Encoding.UTF8.GetString(datos));
            ws.OnError += _ => Error?.Invoke("Se perdió la conexión con el servidor.");
            ws.OnClose += codigo =>
            {
                if (codigo != WebSocketCloseCode.Normal)
                    Error?.Invoke("El servidor cerró la conexión.");
            };

            // NO hacer await: en NativeWebSocket, Connect() no regresa hasta que la conexión se cierra.
            _ = ws.Connect();
        }

        public void Enviar(string tipo, object datos)
        {
            if (ws == null || ws.State != WebSocketState.Open)
            {
                Error?.Invoke("No hay conexión con el servidor.");
                return;
            }
            _ = ws.SendText(JsonConvert.SerializeObject(new { type = tipo, data = datos }));
        }

        public void Despachar()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
        }

        public void Cerrar()
        {
            if (ws == null) return;
            var actual = ws;
            ws = null;
            if (actual.State == WebSocketState.Open || actual.State == WebSocketState.Connecting)
                _ = actual.Close();
        }

        void Procesar(string json)
        {
            JObject sobre;
            try
            {
                sobre = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"Mensaje inválido del servidor: {e.Message}");
                return;
            }

            var tipo = (string)sobre["type"];
            var datos = sobre["data"];
            if (datos == null || datos.Type == JTokenType.Null)
            {
                Debug.LogWarning($"Mensaje '{tipo}' sin datos.");
                return;
            }

            try
            {
                switch (tipo)
                {
                    case TiposMensaje.Estado: EstadoRecibido?.Invoke(datos.ToObject<EstadoPartidaMsg>()); break;
                    case TiposMensaje.JugadorMovido: JugadorMovido?.Invoke(datos.ToObject<JugadorMovidoMsg>()); break;
                    case TiposMensaje.JugadorActuo: JugadorActuo?.Invoke(datos.ToObject<JugadorActuoMsg>()); break;
                    case TiposMensaje.PuntajeCambiado: PuntajeCambiado?.Invoke(datos.ToObject<PuntajeMsg>()); break;
                    case TiposMensaje.Tick: Tick?.Invoke(datos.ToObject<TickMsg>()); break;
                    case TiposMensaje.TiempoExtra: TiempoExtra?.Invoke(datos.ToObject<TiempoExtraMsg>()); break;
                    case TiposMensaje.PartidaFinalizada: PartidaFinalizada?.Invoke(datos.ToObject<PartidaFinalizadaMsg>()); break;
                    case TiposMensaje.Error: Error?.Invoke((string)datos["mensaje"] ?? "Error del servidor."); break;
                    default: Debug.LogWarning($"Tipo de mensaje desconocido: {tipo}"); break;
                }
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"No se pudo leer el mensaje '{tipo}': {e.Message}");
            }
        }
    }
}
