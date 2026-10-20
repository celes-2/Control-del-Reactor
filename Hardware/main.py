import time
import config
from wifi import ConexionWifi
from botones import LectorBotones
from cliente_mqtt import ClienteMqtt

PING_MS = 10000   # cada 10 s le avisa al broker que sigue viva


def conectar_todo(wifi, mqtt, es_reconexion):
    """Conecta WiFi y MQTT; si falla, reintenta hasta lograrlo."""
    while True:
        try:
            wifi.conectar()
            if es_reconexion:
                mqtt.reconectar()
            else:
                mqtt.conectar()
            return
        except OSError as error:
            print("No se pudo conectar:", error, "- reintentando en 2 s")
            es_reconexion = True
            time.sleep(2)


def main():
    wifi = ConexionWifi(config.WIFI_SSID, config.WIFI_PASSWORD)
    mqtt = ClienteMqtt(
        broker=config.MQTT_BROKER,
        puerto=config.MQTT_PORT,
        device_id=config.DEVICE_ID,
        usuario=getattr(config, "MQTT_USER", None),
        password=getattr(config, "MQTT_PASSWORD", None),
    )
    botones = LectorBotones(debounce_ms=50)

    conectar_todo(wifi, mqtt, es_reconexion=False)
    ultimo_ping = time.ticks_ms()
    print("Control listo.")

    while True:
        try:
            for boton in botones.leer_presionados():
                mqtt.publicar_evento(boton)

            if time.ticks_diff(time.ticks_ms(), ultimo_ping) >= PING_MS:
                mqtt.ping()
                ultimo_ping = time.ticks_ms()

        except OSError as error:
            print("Conexion perdida:", error)
            conectar_todo(wifi, mqtt, es_reconexion=True)
            ultimo_ping = time.ticks_ms()

        time.sleep_ms(10)


main()