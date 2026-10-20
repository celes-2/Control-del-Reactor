# Contrato WebSocket — Unity ↔ servidor (US-13)

Estado: **confirmado por Palma y Celeste el 20-oct-2026.**
Cualquier cambio se avisa a todo el equipo antes de tocar código (regla 2.6 del contexto).

## Por qué WebSocket

Un movimiento debe verse de inmediato en el otro equipo y en ambos sentidos. HTTP con polling añade
latencia y carga, y Server-Sent Events solo va del servidor al cliente. El WebSocket nativo de
ASP.NET Core mantiene un canal bidireccional por jugador, sin recargar la pantalla, y el servidor
conoce la identidad de cada conexión (el token se valida al conectar). Cada partida es una sala.

## Conexión

```
ws://IP_SERVIDOR:8080/ws/partida?token=JWT
```

- Mientras no exista el login con JWT: `?userId=ID` provisional (igual que el `UsuarioId` en el cuerpo de la API REST).
- Al abrir el canal, el cliente envía `match:join` con el id de la partida.
- Si `match:join` llega para una partida a la que el jugador ya está unido, el servidor solo **reenvía `match:state`**
  (Unity cambia de la sala de espera a la arena llamando otra vez a `Conectar` con el mismo id).
- Si llega de alguien que **no** está en la partida: en `Espera` el servidor lo une (`UnirsePartida`: exige correo
  verificado y avatar, y que no esté llena); si la partida ya empezó o terminó, responde `error`.

## Formato común

Todo mensaje, en los dos sentidos:

```json
{ "type": "...", "data": { ... } }
```

Nombres de campo en inglés y `camelCase`. Los ids son texto (GUID). Posiciones: origen `(0,0)` arriba a la izquierda,
`y` crece hacia abajo.

## Cliente → servidor

| type | data | Quién |
|---|---|---|
| `match:join` | `{ "matchId": "..." }` | cualquier jugador |
| `match:start` | `{}` | solo el anfitrión, con 2 o más jugadores |

Unity **nunca** envía movimientos: los botones llegan desde la Pico por MQTT. El servidor decide.

## Servidor → cliente

| type | data |
|---|---|
| `match:state` | `{ matchId, hostId, status, endMode, endValue, timeLeft, timeElapsed, scenario, players }` |
| `player:moved` | `{ userId, x, y, score }` |
| `player:acted` | `{ userId, message }` (message opcional) |
| `score:changed` | `{ userId, score }` |
| `match:tick` | `{ timeLeft, timeElapsed }` (una vez por segundo) |
| `match:overtime` | `{ extraMinutes, timeLeft }` (empate: +2 minutos y sigue con el puntaje, CA-16.4) |
| `match:finished` | `{ winnerId, scores }` (`winnerId` puede ser `null`) |
| `error` | `{ message }` (solo al jugador que lo causó) |

### `match:state`

```json
{
  "type": "match:state",
  "data": {
    "matchId": "7c1e...",
    "hostId": "a1b2...",
    "status": "Espera",
    "endMode": "TIME",
    "endValue": 60,
    "timeLeft": 60,
    "timeElapsed": 0,
    "scenario": {
      "width": 15,
      "height": 11,
      "rows": ["WWWWWWWWWWWWWWW", "WS...........SW", "..."]
    },
    "players": [
      { "userId": "a1b2...", "username": "Ana", "avatarId": "cc2e...", "x": 1, "y": 1, "score": 0 }
    ]
  }
}
```

- `status`: `Espera` | `EnCurso` | `Finalizada`.
- `endMode`: `TIME` | `POINTS` (en `TIME`, `endValue` son los segundos de duración; en `POINTS`, la meta de puntos).
- En modo `POINTS`, `timeLeft` va en `null` (en `match:state` y en `match:tick`) y Unity muestra `timeElapsed`, contando hacia arriba.
- `scenario.rows`: una cadena por fila. `W` muro, `.` piso, `S` inicio de un jugador, `R` reactor, `T` trampa.
- `players[].color` y `players[].avatarUrl` son opcionales. Si no hay `color`, Unity asigna uno por orden de llegada.
- Se envía al unirse, al cambiar la lista de jugadores, al iniciar y cuando un cliente repite `match:join`.

### `match:finished`

```json
{
  "type": "match:finished",
  "data": {
    "winnerId": "a1b2...",
    "scores": [
      { "userId": "a1b2...", "username": "Ana", "score": 14 },
      { "userId": "c3d4...", "username": "Beto", "score": 9 }
    ]
  }
}
```

### Orden de los mensajes al terminar

Si un movimiento lleva a alguien a la meta de puntos, el servidor envía primero `player:moved` (y `score:changed`)
y enseguida `match:finished`. Unity deja ver el último movimiento unos segundos antes de abrir la pantalla de victoria.

## Hardware (referencia)

La Pico publica en MQTT `arena/devices/{deviceId}/events` → `{"button":"UP","seq":42}` con
`UP | DOWN | LEFT | RIGHT | ACTION`. El servidor la traduce a una acción de juego y difunde `player:moved` o `player:acted`.

## Qué cambia en el servidor respecto a hoy

- `PartidaVista` ya incluye `Escenario` (filas) desde el cambio de B7.
- `match:state` se arma desde `PartidaVista` más la lista de jugadores y el anfitrión.
- El servidor llama a `Partida.Tick` una vez por segundo y emite `match:tick`; si el tiempo termina en empate,
  `Partida` queda en curso con 120 s más (`Partida.Prorrogas` aumenta) y se emite `match:overtime`.
- `timeElapsed` necesita un contador en `Partida` (`SegundosTranscurridos`), que se suma en cada `Tick`.
