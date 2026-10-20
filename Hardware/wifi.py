import network
import time
from machine import Pin


class ConexionWifi:
    """Se encarga solo de conectar la Pico W a la red WiFi."""

    def __init__(self, ssid, password):
        self._ssid = ssid
        self._password = password
        self._wlan = network.WLAN(network.STA_IF)
        self._led = Pin("LED", Pin.OUT)

    def esta_conectada(self):
        return self._wlan.isconnected()

    def conectar(self):
        self._wlan.active(True)
        if self.esta_conectada():
            return

        print("Conectando a", self._ssid, "...")
        self._wlan.connect(self._ssid, self._password)

        while not self.esta_conectada():
            self._led.toggle()        # parpadea mientras intenta
            time.sleep_ms(250)

        self._led.off()
        print("WiFi conectado. IP:", self._wlan.ifconfig()[0])