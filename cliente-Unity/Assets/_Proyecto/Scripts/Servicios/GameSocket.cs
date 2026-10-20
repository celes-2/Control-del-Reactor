using System;
using System.Text;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reactor.Cliente.Contratos;
using UnityEngine;

namespace Reactor.Cliente.Servicios
{
    /// <summary>
    /// Cliente WebSocket real contra el servidor ASP.NET Core (contrato en Docs/contratos-websocket.md).
    /// Todos los mensajes viajan en un sobre: { "type": "...", "data": { ... } }.
    /// URL: ws://IP:8080/ws/partida?token=JWT  (provisional, sin login: ?userId=ID).
    /// </summary>
    public sealed class GameSocket : IGameSocket
    {
        readonly string url;
        readonly SessionStore sesion;
        WebSocket ws;
        string partidaActual;

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
            // Al cambiar de escena (sala de espera -> arena) se vuelve a llamar con la misma partida:
            // no se reconecta, solo se repite match:join y el servidor reenvía match:state.
            if (ws != null && partidaActual == partidaId)
            {
                if (ws.State == WebSocketState.Open)
                {
                    Conectado?.Invoke();
                    Enviar(TiposMensaje.Unirse, new UnirseMsg { PartidaId = partidaId });
                    return;
                }
                if (ws.State == WebSocketState.Connecting) return; // al abrir enviará match:join
            }

            Cerrar();
            partidaActual = partidaId;

            ws = new WebSocket(ConstruirUrl());
            ws.OnOpen += () =>
            {
                Conectado?.Invoke();
                Enviar(TiposMensaje.Unirse, new UnirseMsg { PartidaId = partidaId });
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
            partidaActual = null;
            if (ws == null) return;
            var actual = ws;
            ws = null;
            if (actual.State == WebSocketState.Open || actual.State == WebSocketState.Connecting)
                _ = actual.Close();
        }

        string ConstruirUrl()
        {
            if (!string.IsNullOrEmpty(sesion.Token))
                return $"{url}?token={Uri.EscapeDataString(sesion.Token)}";
            if (!string.IsNullOrEmpty(sesion.UsuarioId))
                return $"{url}?userId={Uri.EscapeDataString(sesion.UsuarioId)}"; // provisional, hasta que exista el login
            return url;
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
                    case TiposMensaje.Error: Error?.Invoke(datos.ToObject<ErrorMsg>()?.Mensaje ?? "Error del servidor."); break;
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
