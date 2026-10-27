# Control-del-Reactor
Plataforma multijugador con integración hardware-software. CE-1105 Principios de Modelado en Ingeniería · TEC · II Semestre 2026 · Sprint 2: Primera implementación (v0.2.0).

En el centro de una arena 2D hay un reactor. Cada jugador mueve su avatar con un control físico de cinco botones (arriba, abajo, izquierda, derecha y acción) basado en una Raspberry Pi Pico W, desde su propia computadora, y ve en tiempo real los movimientos del otro.

Qué demuestra el Sprint 2 (vertical slice)
Usuario -> autenticación -> partida -> servidor -> hardware -> comunicación -> avatar -> arena
Registro con correo, verificación por código de 6 dígitos, inicio de sesión (JWT) y recuperación de contraseña.
Avatares (5 por defecto o imagen propia) y perfil con historial, estadísticas, logros y ranking.
Crear, configurar y unirse a partidas (fin por tiempo o por puntos, empate con prórroga de 2 minutos).
Dos jugadores en computadoras distintas, cada uno controlando solo su avatar con su Pico W.
Puntaje, ganador automático y pantalla de victoria.
Datos persistentes entre reinicios de los contenedores.
Tecnologías
Parte	Tecnología
Servidor	C#, ASP.NET Core (.NET 10), xUnit
Cliente	Unity 6.6 (6000.6.0f1), Universal 2D, build de escritorio Windows
Tiempo real	WebSocket nativo de ASP.NET Core + JSON (NativeWebSocket en Unity)
Hardware	Raspberry Pi Pico WH, MicroPython 1.29.0, umqtt
Broker	Eclipse Mosquitto 2 (con usuario y contraseña)
Persistencia	Archivos JSON en el volumen Docker reactor-data
Seguridad	BCrypt (contraseñas), JWT (sesión)
Correo	MailKit (Gmail con contraseña de aplicación); MailHog en desarrollo
DevOps	Docker, Docker Compose, Azure Pipelines
Estructura del repositorio
.github/            plantilla de pull request
Docs/               documentación (arquitectura, contratos, patrones, DevOps) e img/ con los diagramas
Hardware/           firmware MicroPython de la Pico W
mosquitto/          configuración del broker (passwd NO se versiona)
Servidor/
  src/              API, Aplicacion, Dominio, Infraestructura, Contratos
  Pruebas/          Pruebas.Dominio, Pruebas_Aplicacion, Pruebas.Infraestructura, Pruebas.API
cliente-Unity/      proyecto de Unity
docker-compose.yml  backend + Mosquitto
docker-compose.dev.yml  agrega MailHog (correo de desarrollo)
azure-pipelines.yml pipeline de CI
.env.example        plantilla de variables (el .env NO se versiona)
CONTRIBUTING.md     flujo de trabajo con Git
Inicio rápido (Docker)

Requisitos: Docker Desktop abierto, y todos los equipos (servidor, clientes y Pico W) en la misma red de 2.4 GHz (la Pico W no usa 5 GHz y la red del TEC puede aislar equipos; usar hotspot o router propio).

powershell
# 1. Variables de entorno (editar los valores)
Copy-Item .env.example .env

# 2. Usuario del broker MQTT (una sola vez)
docker run --rm -it -v "${PWD}/mosquitto:/mosquitto/config" eclipse-mosquitto:2 mosquitto_passwd -c /mosquitto/config/passwd arena
docker run --rm --user root -v "${PWD}/mosquitto:/mosquitto/config" eclipse-mosquitto:2 sh -c "chown 1883:1883 /mosquitto/config/passwd && chmod 0700 /mosquitto/config/passwd"

# 3. Levantar
docker compose up -d
docker compose ps
Invoke-RestMethod http://localhost:8080/api/avatares   # debe devolver 5 avatares

Para probar sin correo real, usar MailHog (los correos se ven en http://localhost:8025):

powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d

La guía completa (firewall, IP del servidor, build de Unity, configuración de las Pico, demo paso a paso y problemas frecuentes) está en Docs/despliegue-y-demo.md.

Nunca usar docker compose down -v: borra el volumen reactor-data y todos los datos.

Desarrollo sin Docker
powershell
dotnet build Servidor/ControlDelReactor.slnx
dotnet test  Servidor/ControlDelReactor.slnx
dotnet run --project Servidor/src/API --urls "http://0.0.0.0:5173"

Sin --urls "http://0.0.0.0:..." el servidor solo escucha en localhost y otras computadoras no pueden conectarse. El .env solo lo lee Docker Compose; con dotnet run las credenciales SMTP van en variables de entorno ($env:Smtp__Host, etc.).

Cómo jugar (demo)
Cada jugador registra su cuenta; le llega un código de 6 dígitos por correo.
Verifica el correo, inicia sesión y elige avatar.
Un jugador crea la partida en Configuración; el otro la ve en Unirse.
El anfitrión inicia. Cada jugador mueve su avatar con su control; el botón ACCION muestra un mensaje que ve el otro.
Al terminar aparece la pantalla de victoria; en Inicio quedan historial, estadísticas, logros y ranking.

El control debe estar emparejado con el jugador: POST /api/devices/pair con {"deviceId":"pico-01"} y el JWT del jugador. Los eventos de un control sin emparejar se descartan (así no se mezclan los controles).

Comunicaciones

REST (resumen; contratos en Servidor/src/Contratos):

Método y ruta	Uso
POST /api/auth/register, /verify-email, /login	Registro, verificación y sesión (JWT de 8 h)
POST /api/auth/forgot-password, /reset-password	Recuperación de contraseña
GET /api/auth/profile, PUT /api/auth/profile/avatar, POST /api/auth/profile/avatar/upload	Perfil y avatar
GET /api/avatares, GET /api/avatares/{id}/imagen	Catálogo e imágenes
POST /api/devices/pair	Emparejar control con jugador
POST/GET /api/partidas, GET /api/partidas/{id}, POST /api/partidas/{id}/unirse, /iniciar	Partidas
GET /api/perfil, /api/perfil/historial, /api/ranking	Perfil, historial y ranking

Errores con formato único: { "error": "...", "codigo": "CREDENCIALES_INVALIDAS", "mensaje": "..." }.

WebSocket ws://IP:8080/ws/partida?token=JWT, sobre { "type": "...", "data": {...} }. Mensajes: match:join, match:start (cliente) y match:state, player:moved, player:acted, score:changed, match:tick, match:overtime, match:finished, error (servidor). Contrato completo en Docs/contratos-websocket.md.

MQTT (control físico): arena/devices/{deviceId}/events (QoS 1) con {"button":"UP","seq":42}; arena/devices/{deviceId}/status retenido, con Last Will.

Hardware

Pico WH con cinco botones y pull-up interna: UP=GP2, DOWN=GP3, LEFT=GP4, RIGHT=GP5, ACTION=GP6, GND en pin 3 u 8. Antirrebote por software de 50 ms y número de secuencia seq para detectar duplicados y pérdidas. Las credenciales van en config.py (no versionado); usar config.example.py como plantilla.

Pruebas y CI
Servidor: pruebas xUnit de dominio, aplicación, infraestructura y API (dotnet test).
CI en Azure Pipelines en cada push a develop, main, feature/* y en cada PR: compilar y probar, imagen Docker y validación de Compose. Una prueba fallida bloquea la integración.
Hardware y multijugador: pruebas manuales guiadas (ver CHECKLIST_CRITERIOS.md).
Flujo de trabajo con Git

Ramas: main (entregas, protegida), develop (integración, protegida) y feature/* (una por módulo). PR hacia develop con 1 aprobación y CI en verde, merge normal. Commits con Conventional Commits y enlace al tablero (feat(US-07): ... AB#107). Detalle en CONTRIBUTING.md y Docs/devops-y-versiones.md.

Con Unity: cerrar el editor antes de git switch o git merge.

Documentación
Documento	Contenido
Docs/arquitectura.md	Estilo, capas, componentes, despliegue y secuencia de un movimiento
Docs/patrones-y-solid.md	Patrones de arquitectura y diseño, y SOLID con ejemplos reales
Docs/devops-y-versiones.md	Ramas, pipeline, Docker y pruebas automáticas
Docs/cliente-unity.md	MVP, escenas y flujo de pantallas
Docs/despliegue-y-demo.md	Guía de despliegue y demo
Docs/contratos-websocket.md	Mensajes WebSocket
Docs/api-rest.md	Endpoints REST, autenticación y códigos de error
Docs/modelo-de-datos.md	Archivos JSON, relaciones y reglas de persistencia
Docs/hardware-y-mqtt.md	Control físico, pines, tópicos MQTT y emparejamiento
Docs/planificacion.md	Cronograma, roles, riesgos y herramientas
Docs/pruebas.md	Pruebas automáticas y manuales
Docs/decisiones-y-sprint-3.md	Decisiones abiertas y trabajo para el Sprint 3
Limitaciones conocidas (Sprint 2)
La persistencia usa archivos JSON (escritura atómica, copia .bak); la partida en curso vive en memoria y se guarda al finalizar. Para el Sprint 3 se evaluará una base de datos detrás de los mismos puertos.
El emparejamiento control-jugador es manual (POST /api/devices/pair).
Trampas, guardianes y extracción de energía del reactor corresponden al Sprint 3.
Equipo
Integrante	Responsabilidad principal
Kendall Vargas	Control 1 y firmware, comunicación en tiempo real (MQTT, WebSocket, reloj de la partida)
Génesis Cubillo	Control 2, pruebas hardware-software, planificación, diagramas y wiki
Fabiola Muñoz	Usuarios y persistencia JSON (registro, verificación, login, perfil, puntaje, ganador)
María Celeste Escobar	Interfaz Unity, dominio y casos de uso de partida, autenticación integrada, Docker y CI

Profesor: Marco Hernández Vásquez · Escuela de Ingeniería en Computadores, TEC.

Seguridad

No se versionan secretos: .env, mosquitto/passwd ni config.py. Solo .env.example y config.example.py. Las contraseñas se guardan con BCrypt y los tokens de verificación solo como hash.
