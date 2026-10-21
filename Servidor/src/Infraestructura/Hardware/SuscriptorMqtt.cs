using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace Infraestructura.Hardware;

/// <summary>Escucha los controles en Mosquitto y entrega cada boton ya traducido.</summary>
public sealed class SuscriptorMqtt(OpcionesMqtt opciones, ILogger<SuscriptorMqtt> log) : IFuenteEventosBoton
{
    private static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(5);

    public async Task EscucharAsync(Func<EventoBoton, Task> alRecibir, CancellationToken ct)
    {
        var fabrica = new MqttClientFactory();
        using var cliente = fabrica.CreateMqttClient();

        cliente.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var texto = e.ApplicationMessage.ConvertPayloadToString();

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

        var opcionesCliente = new MqttClientOptionsBuilder()
            .WithTcpServer(opciones.Host, opciones.Puerto)
            .WithClientId(opciones.ClientId)
            .Build();

        var suscripcion = fabrica.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(TraductorEventoBoton.TopicSuscripcion, MqttQualityOfServiceLevel.AtLeastOnce)
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
                    log.LogInformation("Conectado a MQTT en {Host}:{Puerto}", opciones.Host, opciones.Puerto);
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