import time
from machine import Pin


class LectorBotones:
    """Lee los 5 botones del control y detecta cada presion con antirrebote."""

    PINES = {
        "UP": 2,
        "DOWN": 3,
        "LEFT": 4,
        "RIGHT": 5,
        "ACTION": 6,
    }

    def __init__(self, debounce_ms=50):
        self._debounce_ms = debounce_ms
        self._pines = {}
        self._estable = {}         # ultimo estado confirmado (1 suelto, 0 presionado)
        self._crudo = {}           # ultima lectura sin filtrar
        self._ultimo_cambio = {}   # momento en que cambio la lectura cruda
        ahora = time.ticks_ms()
        for nombre, gpio in self.PINES.items():
            self._pines[nombre] = Pin(gpio, Pin.IN, Pin.PULL_UP)
            self._estable[nombre] = 1
            self._crudo[nombre] = 1
            self._ultimo_cambio[nombre] = ahora

    def leer_presionados(self):
        """Devuelve la lista de botones que se acaban de presionar."""
        ahora = time.ticks_ms()
        presionados = []
        for nombre, pin in self._pines.items():
            valor = pin.value()

            if valor != self._crudo[nombre]:
                self._crudo[nombre] = valor
                self._ultimo_cambio[nombre] = ahora

            quieto = time.ticks_diff(ahora, self._ultimo_cambio[nombre]) >= self._debounce_ms
            if quieto and valor != self._estable[nombre]:
                self._estable[nombre] = valor
                if valor == 0:
                    presionados.append(nombre)
        return presionados