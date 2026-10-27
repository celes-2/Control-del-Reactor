import json
import time
from machine import Pin
from umqtt.simple import MQTTClient


class ClienteMqtt:
    """Publica los eventos del control en el broker MQTT."""

    def __init__(self, broker, puerto, device_id, usuario=None, password=None):
        self._topic_eventos = "arena/devices/" + device_id + "/events"
        self._topic_estado = "arena/devices/" + device_id + "/status"
        self._seq = 0
        self._led = Pin("LED", Pin.OUT)
        self._cliente = MQTTClient(
            client_id=device_id,
            server=broker,
            port=puerto,
            user=usuario,
            password=password,
            keepalive=30,
        )
        # Si la Pico se desconecta sin avisar, el broker publica "offline" por ella
        self._cliente.set_last_will(self._topic_estado, "offline", retain=True, qos=1)

    def conectar(self):
        self._cliente.connect()
        self._cliente.publish(self._topic_estado, "online", retain=True, qos=1)
        print("MQTT conectado. Publicando en", self._topic_eventos)

    def publicar_evento(self, boton):
        self._seq += 1
        mensaje = json.dumps({"button": boton, "seq": self._seq})
        self._cliente.publish(self._topic_eventos, mensaje, qos=1)
        self._led.on()               # destello corto = mensaje enviado
        time.sleep_ms(30)
        self._led.off()
        print("Enviado:", mensaje)

    def ping(self):
        self._cliente.ping()

    def reconectar(self):
        try:
            self._cliente.disconnect()
        except OSError:
            pass                     # si ya estaba caida, no importa
        self.conectar()