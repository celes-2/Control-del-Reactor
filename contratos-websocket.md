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
| `match:leave` | `{}` | cualquier jugador, solo en la sala de espera ("Volver al perfil"). Si sale el anfitrión (o no queda nadie) la partida se elimina y los demás reciben un `error` "La partida se cerró"; si sale otro jugador, la sala recibe `match:state` sin él |
| `match:watch` | `{ "matchId": "..." }` | el móvil (US-30): mira sin unirse; recibe `match:state` y los mensajes de la sala, no los rechazos privados |
| `player:input` | `{ "button": "UP" \| "DOWN" \| "LEFT" \| "RIGHT" \| "ACTION" }` | el jugador en curso: el teclado de Unity. Hace lo mismo que el botón de la Pico (mismas reglas y mismas respuestas `player:moved`, `score:changed`...) y convive con el control físico |
| `device:pair` | `{}` | cualquier jugador conectado: "vincular mi control". El PRIMER botón que llegue de cualquier control (en los próximos 60 s) queda asociado a este jugador y no mueve a nadie. Responde `device:paired`. Sirve sin importar el número del control ni el orden de entrada |
| `device:pair-cancel` | `{}` | cancela la petición anterior |

Unity **nunca** envía movimientos: los botones llegan desde la Pico por MQTT. El servidor decide.

`match:watch` (US-30, Palma) es **aditivo**: Unity no lo usa y no cambia nada de lo anterior. La conexión del móvil
entra como espectador, aparte de los jugadores, así que **no reemplaza** la conexión de Unity del mismo usuario.
El móvil obtiene el id de su partida con `GET /api/partidas/activa` (204 si no está en ninguna).

## Servidor → cliente

| type | data |
|---|---|
| `match:state` | `{ matchId, hostId, status, endMode, endValue, timeLeft, timeElapsed, scenario, players }` |
| `device:paired` | `{ "deviceId": "pico-02" }` | solo al jugador que pidió vincular; el control quedó asignado a él (se guarda en dispositivos.json) |
| `player:moved` | `{ userId, x, y, score }` |
| `player:acted` | `{ userId, message }` (message opcional) |
| `score:changed` | `{ userId, score }` |
| `match:tick` | `{ timeLeft, timeElapsed }` (una vez por segundo) |
| `match:overtime` | `{ extraMinutes, timeLeft }` (empate: +2 minutos y sigue con el puntaje, CA-16.4) |
| `match:finished` | `{ winnerId, scores }` (`winnerId` puede ser `null`) |
| `error` | `{ message }` (solo al jugador que lo causó) |
| `arena:update` | `{ timeElapsed, live }` (una vez por segundo con la partida en curso; Sprint 3, ver abajo) |
| `event:started` | `{ eventId, kind, duration, message }` |
| `event:ended` | `{ eventId, kind }` |
| `player:frozen` | `{ userId, seconds, source, trapKind }` |
| `item:collected` | `{ itemId, userId, points }` (llega junto con `score:changed`) |
| `weapon:fired` | `{ userId, guardianIds, stunSeconds, cooldown }` (arma paralizadora, a toda la sala) |

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

## Arena viva (Sprint 3, Paquete B — Faby)

Reglas completas en `Docs/arena-viva.md`. Todo lo nuevo es **aditivo**: los campos y mensajes de antes no cambian.

### Al crear la partida (REST)

- `GET /api/escenarios` → `[{ id, nombre, descripcion, ancho, alto, filas, puertas: [{x,y}], inicioGuardianes: [{x,y}],
  maxJugadores, minGuardianes: 2, maxGuardianes: 4, guardianesPorDefecto: 4, trampasAleatorias, puntosEnergia: [{x,y}] }]`.
  Ids: `laboratorio`, `estacion`, `militar` (los tres de 30×20). Las posiciones de las trampas se conocen recién al
  iniciar, en `live.traps`.
- `POST /api/partidas` acepta además `escenarioId` (null = mapa clásico) y `cantidadGuardianes` (2 a 4, null = 4).
  Fuera de rango o id desconocido → 400. La respuesta trae `escenarioId` y `cantidadGuardianes`.

### `match:state` (campos nuevos)

- `scenarioId`, `guardianCount`.
- `players[].frozenSeconds`: 0 = normal; mayor que 0 = congelado.
- `live`: la misma estructura que `arena:update` (null mientras la partida está en `Espera`).
- `scenario.rows` sigue con `W . S R T`: una puerta cerrada viaja como `W` y una abierta como `.`; los inicios de
  guardián como `.`. Para dibujar puertas, guardianes, trampas por tipo e ítems, usar `live`.

### `live` / `arena:update`

```json
{
  "type": "arena:update",
  "data": {
    "timeElapsed": 42,
    "live": {
      "guardians": [ { "id": 1, "x": 7, "y": 2, "temporary": false, "paused": false } ],
      "traps":     [ { "x": 4, "y": 4, "kind": "QUICKSAND" } ],
      "doors":     [ { "x": 5, "y": 2, "open": true } ],
      "items":     [ { "id": 3, "x": 9, "y": 7, "points": 25, "secondsLeft": 6 } ],
      "events":    [ { "id": "9f1c...", "kind": "REACTOR_OVERLOAD", "duration": 10, "secondsLeft": 4 } ],
      "frozen":    [ { "userId": "a1b2...", "seconds": 2 } ],
      "scoreMultiplier": 1
    }
  }
}
```

- `traps[].kind`: `QUICKSAND` (tierra movediza, 3 s) · `RISING_CELL` (celda emergente, 5 s) · `NET` (red, 2 s). Todas restan 5 puntos.
- `guardians[].temporary`: llegó con la invasión. `paused`: acaba de congelar a alguien y está quieto.
  `stunnedSeconds`: segundos que sigue paralizado por el arma de un jugador (0 = normal); paralizado no se mueve ni congela.
- `events[].kind`: `EXTRA_ENERGY` · `GUARDIAN_INVASION` · `REACTOR_OVERLOAD` · `DOUBLE_POINTS` · `ALTERNATE_ROUTES` · `ENERGY_RAIN`.
- `scoreMultiplier`: 2 durante «Puntos dobles».

### Mensajes puntuales

- `event:started` → `{ "eventId": "...", "kind": "REACTOR_OVERLOAD", "duration": 10, "message": "¡Sobrecarga! El reactor no da energía durante 10 s." }`.
  `message` está listo para mostrar en pantalla.
- `event:ended` → `{ "eventId": "...", "kind": "REACTOR_OVERLOAD" }`.
- `player:frozen` → `{ "userId": "...", "seconds": 5, "source": "TRAP", "trapKind": "RISING_CELL" }`.
  `source`: `GUARDIAN` | `TRAP`; `seconds` es el total que le queda (si ya estaba congelado, se suma).
- `item:collected` → `{ "itemId": 3, "userId": "...", "points": 25 }`.
- `weapon:fired` → `{ "userId": "...", "guardianIds": [2, 7], "stunSeconds": 5, "cooldown": 15 }`.
  `guardianIds` puede venir vacío (disparó sin nadie cerca; igual entra en recarga).

### Arma paralizadora

- Se dispara con **ACCIÓN lejos del reactor** (el mismo `ACTION` del control y la misma tecla de extraer en Unity).
  Junto al reactor, ACCIÓN sigue extrayendo como siempre.
- En partidas con arena viva, ACCIÓN lejos del reactor ya **no** responde `LEJOS_DEL_REACTOR`: dispara (`weapon:fired`)
  o, si está recargando, responde `player:rejected` con `code: "ARMA_RECARGANDO"` solo a ese jugador.
- `match:state` agrega `players[].weaponCooldown` (segundos para volver a disparar; 0 = lista).
- Reglas completas: `Docs/arena-viva.md`, sección «Arma paralizadora».

### Orden

- Al mover: `player:moved` → `score:changed` → (`player:frozen` / `item:collected` / `event:ended`) → `match:finished` si terminó.
- Cada segundo: `match:tick` → (`event:started` / `event:ended` / `player:frozen`) → `arena:update` → `match:overtime` / `match:finished`.

## Hardware (referencia)

La Pico publica en MQTT `arena/devices/{deviceId}/events` → `{"button":"UP","seq":42}` con
`UP | DOWN | LEFT | RIGHT | ACTION`. El servidor la traduce a una acción de juego y difunde `player:moved` o `player:acted`.

## Qué cambia en el servidor respecto a hoy

- `PartidaVista` ya incluye `Escenario` (filas) desde el cambio de B7.
- `match:state` se arma desde `PartidaVista` más la lista de jugadores y el anfitrión.
- El servidor llama a `Partida.Tick` una vez por segundo y emite `match:tick`; si el tiempo termina en empate,
  `Partida` queda en curso con 120 s más (`Partida.Prorrogas` aumenta) y se emite `match:overtime`.
- `timeElapsed` necesita un contador en `Partida` (`SegundosTranscurridos`), que se suma en cada `Tick`.