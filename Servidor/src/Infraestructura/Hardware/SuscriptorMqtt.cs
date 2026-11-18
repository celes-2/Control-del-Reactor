using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace Infraestructura.Hardware;

/// <summary>Escucha los controles en Mosquitto: entrega cada boton ya traducido y avisa cuando un control se conecta.</summary>
public sealed class SuscriptorMqtt(OpcionesMqtt opciones, ILogger<SuscriptorMqtt> log) : IFuenteEventosBoton
{
    private static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(5);

    public async Task EscucharAsync(Func<EventoBoton, Task> alRecibir, Func<string, Task> alConectarse, CancellationToken ct)
    {
        var fabrica = new MqttClientFactory();
        using var cliente = fabrica.CreateMqttClient();

        cliente.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var texto = e.ApplicationMessage.ConvertPayloadToString();

            // Estado del control ("online" / "offline"): no es un boton.
            if (TraductorEventoBoton.EsTopicEstado(topic))
            {
                var enLinea = TraductorEventoBoton.ControlEnLinea(topic, texto);

                // Solo cuenta un "online" EN VIVO. El retenido llega cada vez que el servidor se
                // (re)suscribe y no significa que la Pico se haya reiniciado.
                if (enLinea is not null && !e.ApplicationMessage.Retain)
                {
                    try
                    {
                        await alConectarse(enLinea);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Error al procesar el estado de {DeviceId}", enLinea);
                    }
                }
                return;
            }

            var evento = TraductorEventoBoton.Traducir(topic, texto);
            if (evento is null)
            {
                log.LogWarning("Mensaje MQTT descartado en {Topic}: {Texto}", topic, texto);
                return;
            }

            try
            {
                await alRecibir(evento);
            }
            catch (Exception ex)
            {
                // Un error con un evento no debe detener la escucha de los demas.
                log.LogError(ex, "Error al procesar el boton de {DeviceId}", evento.DeviceId);
            }
        };

        var constructor = new MqttClientOptionsBuilder()
            .WithTcpServer(opciones.Host, opciones.Port)
            .WithClientId(opciones.ClientId);

        // Solo usa usuario y clave si estan configurados (Mosquitto con allow_anonymous false).
        if (!string.IsNullOrWhiteSpace(opciones.Username))
            constructor = constructor.WithCredentials(opciones.Username, opciones.Password);

        var opcionesCliente = constructor.Build();

        var suscripcion = fabrica.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(TraductorEventoBoton.TopicSuscripcion, MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter(TraductorEventoBoton.TopicEstado, MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        // Cada 5 s revisa la conexion; si se cayo, se reconecta y se vuelve a suscribir.
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!cliente.IsConnected)
                {
                    await cliente.ConnectAsync(opcionesCliente, ct);
                    await cliente.SubscribeAsync(suscripcion, ct);
                    log.LogInformation("Conectado a MQTT en {Host}:{Port}", opciones.Host, opciones.Port);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning("No se pudo conectar a MQTT ({Mensaje}). Reintento en 5 s.", ex.Message);
            }

            try
            {
                await Task.Delay(EsperaReintento, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}