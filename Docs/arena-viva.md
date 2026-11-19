# Arena viva — reglas del Paquete B (Sprint 3)

US-20 obstáculos y trampas · US-21 guardianes y congelado · US-22 eventos especiales · US-23 escenarios.
Responsable: Faby. Código: `Servidor/src/Dominio/Juego/ArenaViva.cs` (orquestador) y `ReglasArenaViva.cs` (valores).
Este documento cumple CA-22.10: activación, duración y efecto de cada evento, y las pruebas que lo verifican.

## 1. Principios

- **El servidor decide todo** (CA-21.8). Unity solo dibuja lo que recibe; no puede congelar, mover guardianes ni sumar puntos.
- **Los puntos solo cambian dentro de `Partida`.** La arena viva nunca toca el puntaje: cuando alguien recoge energía,
  la arena viva quita el ícono y la `Partida` suma los puntos.
- **Tiempo:** el reloj del servidor llama `Partida.Tick(1)` cada segundo. Todas las duraciones se miden con
  `Partida.SegundosTranscurridos`, nunca con la hora real. El reloj (`TimeProvider` en `ServicioReloj`) y el azar
  (`IFuenteAzar` en `IniciarPartida`) son inyectables: con la misma semilla la partida es idéntica.
- La arena viva se activa al **iniciar** la partida (`IniciarPartida`) y se apaga al **terminar**: ningún evento sigue
  afectando una partida finalizada (CA-22.8).

## 2. Puntos

| Acción | Efecto en el puntaje |
|---|---|
| Moverse | 0 |
| Extraer del reactor | +1 por unidad de energía (una extracción completa = +10). ×2 durante «Puntos dobles» |
| Caer en una trampa | −5 (nunca baja de 0). No quita vida ni elimina |
| Ser alcanzado por un guardián | 0 (CA-21.9) |
| Tomar energía adicional | +25 (×2 durante «Puntos dobles») |
| Tomar una celda de lluvia de energía | +10 (×2 durante «Puntos dobles») |

## 3. Obstáculos (US-20)

| Obstáculo | Símbolo en el escenario | Regla |
|---|---|---|
| Muro / barrera | `W` | No se atraviesa nunca (jugadores ni guardianes). |
| Puerta automática | `D` | Cambia de estado cada **6 s**. Cerrada = muro; abierta = piso. Al iniciar, las puertas se alternan (la 1.ª cerrada, la 2.ª abierta, …) y **cada una cambia en un segundo distinto** (desfase de 1 s entre puertas), así no se abren y cierran todas a la vez. **Nunca se cierra con alguien encima**: espera a que la celda quede libre y lo vuelve a intentar cada segundo (CA-20.8). |
| Trampa aleatoria | `t` (celda preferida, opcional) | Al iniciar, el servidor reparte al azar **las trampas que indica el JSON del escenario** (14, 18 o 24; 3 en el mapa clásico), con las **3 variantes repartidas parejo** (CA-20.9). |
| Trampa fija | `T` | Solo para mapas dibujados a mano (los escenarios del catálogo no las usan). Es tierra movediza. |

**Variantes de trampa.** Todas inmovilizan y restan 5 puntos; cambia cuánto inmovilizan.

| Variante | Código WebSocket | Inmoviliza |
|---|---|---|
| Tierra movediza | `QUICKSAND` | 3 s (`Config.SegundosCongelamiento`) |
| Celda emergente (surge del piso) | `RISING_CELL` | 5 s (+2) |
| Red inmovilizadora | `NET` | 2 s (−1) |

**Dónde caen las aleatorias:**
- primero en las celdas marcadas con `t` (por ejemplo, los cuellos de botella de la estación) y después en el resto del piso, siempre al azar;
- en piso libre: nunca sobre un muro, una puerta, una salida, el reactor, un inicio de guardián, un punto de energía (`E`) ni otra trampa;
- **nunca pegadas al reactor ni a una salida**: a 2 o más pasos del reactor y de los inicios de guardián, y a 3 o más de los inicios de jugador;
- **separadas entre sí** por la distancia mínima del escenario (`distanciaMinimaEntreTrampas`). Si el mapa no alcanza, se acercan lo justo; si aun así no caben, la partida no inicia (error) y queda en espera.

## 4. Guardianes y congelado (US-21)

- **Cantidad:** el anfitrión elige **de 2 a 4** al crear la partida (`cantidadGuardianes`); el valor por defecto es **4**.
  La demo y las pruebas de aceptación usan 4 (CA-21.1 pide «al menos cuatro»).
- **Dónde empiezan:** en los extremos del campo (celdas `G` del escenario: arriba, abajo, izquierda y derecha).
  Con 2 salen en extremos opuestos (arriba y abajo); con 3, arriba, derecha y abajo. En el mapa clásico (sin `G`)
  se usan los puntos medios de los cuatro bordes.
- **Comportamiento (perseguir):** cada **2 s** dan **un paso** (1 celda) por el camino más corto (búsqueda en anchura)
  hacia el **jugador no congelado más cercano**. Si ese jugador está congelado, van por otro; si todos están congelados,
  se quedan quietos (CA-21.11). No atraviesan muros, puertas cerradas, el reactor, a otros guardianes ni a jugadores.
  Un guardián también bloquea el paso de los jugadores.
- **Contacto:** cuando un guardián queda **a 1 celda** de un jugador (porque el guardián avanzó o porque el jugador se
  acercó), lo **congela 3 s** y luego el guardián se queda **quieto 4 s**. Si el jugador ya estaba congelado (por una
  trampa, por ejemplo), **se suman** los segundos (CA-21.10). El puntaje no cambia (CA-21.9).
- **Estados del jugador** (CA-21.4): *normal* (`frozenSeconds = 0`) y *congelado* (`frozenSeconds > 0`). Congelado no
  puede moverse ni extraer (el servidor responde `Congelado` / `JUGADOR_CONGELADO`) y se recupera solo al llegar a 0
  (CA-21.5, CA-21.6). Todos reciben `player:frozen` y el campo `frozen` de `arena:update` (CA-21.7).

### Arma paralizadora (defensa contra los guardianes)

Pedido del profesor en clase (verbal, Sprint 3); no tiene un criterio escrito en el enunciado. Reglas definidas por
Faby el 18 nov 2026:

| Regla | Valor |
|---|---|
| Cómo se dispara | Botón **ACCIÓN** (el mismo del control físico) **lejos del reactor**. Junto al reactor, ACCIÓN sigue extrayendo. |
| A quién afecta | Solo a **guardianes** (normales y de la invasión) a **2 pasos o menos** del jugador, en cualquier dirección (es una onda: no la tapan los muros). Nunca a jugadores. |
| Efecto | El guardián queda **paralizado 5 s**: no se mueve ni congela a nadie, aunque un jugador quede pegado a él. Un segundo disparo no suma: deja la parálisis otra vez en 5 s. |
| Recarga | Después de disparar, ese jugador espera **15 s**. El disparo se gasta aunque no haya ningún guardián cerca. Mientras recarga, ACCIÓN lejos del reactor responde `ARMA_RECARGANDO`. Cada jugador tiene su propia recarga. |
| Puntos | No suma ni resta puntos. |
| Restricciones | Un jugador congelado no puede disparar. Sin arena viva (partidas sin `IniciarPartida` con azar), ACCIÓN lejos del reactor se rechaza como antes (`LEJOS_DEL_REACTOR`). |

Se avisa a todos con `weapon:fired` (quién disparó, qué guardianes quedaron paralizados, la parálisis y la recarga).
Cada guardián lleva `stunnedSeconds` en `live.guardians` y cada jugador `weaponCooldown` en `match:state`.

## 5. Eventos especiales (US-22)

**Activación.** Al iniciar se arma un calendario con un lote de eventos en orden aleatorio, repartidos a lo largo de la
duración de la partida. La energía adicional, la invasión y los dos creativos ocupan 9 lugares: el evento *i* cae cerca
del segundo `hueco × (i+1)` (con `hueco = duración / 10`, unos 18 s en una partida de 3 minutos), corrido al azar hasta
±¼ de hueco. La **sobrecarga** tiene su propia ventana para que se vea en todas las partidas: la **primera** cae al azar
entre el **11 % y el 25 %** de la duración (**segundos 20 a 45** en una partida de 3 minutos) y la **segunda** en la
segunda mitad (**segundos 90 a 153**). Entre dos eventos siempre hay al menos 2 s y ninguno cae en el primer segundo.
Cuando el lote se acaba (prórroga por empate o modo por puntos) se programa otro igual.
**Sin repeticiones accidentales** (CA-22.9): un evento del mismo tipo nunca está activo dos veces a la vez; si le toca
empezar y ya está activo, se pospone 2 s.

| Evento | Código | Veces por lote | Duración | Efecto | Al terminar |
|---|---|---|---|---|---|
| Energía adicional | `EXTRA_ENERGY` | 6 | 8 s | Aparece un ícono en uno de los puntos `E` del escenario, elegido al azar (si no hay puntos `E` libres, en cualquier piso libre). El primero que lo pisa gana **+25**. | Si nadie lo tomó, desaparece. Si alguien lo tomó, el evento termina en ese momento. |
| Sobrecarga del reactor | `REACTOR_OVERLOAD` | 2 (1.ª entre los s 20 y 45; 2.ª entre los s 90 y 153) | 10 s | Se llama `Partida.DesactivarReactor(10)`: nadie puede extraer (`REACTOR_DESACTIVADO`). | El reactor vuelve a funcionar. |
| Invasión de guardianes | `GUARDIAN_INVASION` | 1 | 30 s | Aparecen **5 guardianes más**, iguales a los normales, lejos de los jugadores y del reactor. | Los 5 temporales desaparecen. |
| Puntos dobles *(creativo 1)* | `DOUBLE_POINTS` | 1 | 15 s | Extraer y recoger energía valen ×2. Las penalizaciones no se duplican. | Todo vuelve a valer lo normal. |
| Rutas alternativas *(creativo 2)* | `ALTERNATE_ROUTES` | 1 | 15 s | Todas las puertas se abren al instante y quedan abiertas. | Las puertas retoman su ciclo de 6 s. |
| Lluvia de energía *(creativo 2, solo arenas sin puertas)* | `ENERGY_RAIN` | 1 | 12 s | Aparecen 3 celdas de energía de +10 cada una. | Las que queden desaparecen (también termina si se toman todas). |

Cada inicio se avisa con `event:started` (tipo, duración y un texto listo para mostrar, CA-22.5) y cada fin con
`event:ended` (CA-22.7). Los eventos activos viajan también en `arena:update` (CA-22.6).

## 6. Escenarios (US-23)

Se eligen al crear la partida (`escenarioId`); la lista sale de `GET /api/escenarios`. Los tres del catálogo miden
**30×20** (Unity hace que la cámara siga al avatar), con 4 inicios de jugador en las esquinas, 4 inicios de guardián
en los extremos (arriba, izquierda, derecha y abajo) y el reactor al centro con sus 4 vecinas libres. Las reglas son las
mismas; cambian los muros, las puertas, los puntos de energía y cuántas trampas se reparten.

| Id | Nombre | Distribución | Puertas | Trampas al azar | Puntos `E` |
|---|---|---|---|---|---|
| `laboratorio` | Laboratorio subterráneo | Pasillos cerrados: 4 laboratorios con puerta y una sala central del reactor con 4 puertas y 2 accesos fijos. | 8 | 14 (separadas ≥3) | 8 |
| `estacion` | Estación espacial | Corredores largos separados por muros, cuellos de botella marcados con `t` y un anillo de pilares alrededor del reactor. | 2 | 18 (separadas ≥2) | 8 |
| `militar` | Complejo militar futurista | Terreno abierto con pilares sueltos y una sola barrera con 2 puertas frente al reactor. | 2 | 24 (separadas ≥3) | 8 |
| *(sin id)* | Arena clásica | El mapa de 15×11 que ya existía, sin puertas. Ahí «Rutas alternativas» se reemplaza por «Lluvia de energía». | 0 | 3 | — |

### Modelo de datos (JSON)

Cada escenario es un archivo en `Servidor/src/Dominio/Juego/Escenarios/<id>.json`, que viaja dentro de `Dominio.dll`.
Para cambiar uno basta con editar su JSON y volver a compilar; para agregar otro, se crea un archivo nuevo.

```json
{
  "id": "estacion",
  "orden": 2,
  "nombre": "Estación espacial",
  "descripcion": "Corredores largos ...",
  "trampasAleatorias": 18,
  "distanciaMinimaEntreTrampas": 2,
  "filas": [ "WWWW…", "WS…G…SW", "…" ]
}
```

Caracteres de `filas`: `W` muro o pilar · `.` piso · `S` salida (inicio de jugador) · `R` reactor · `D` puerta automática ·
`G` inicio de guardián · `E` punto donde aparece la energía · `t` celda preferida para trampas · `T` trampa fija.
`G`, `E` y `t` son piso para los jugadores, y Unity las recibe como `.`.

**Validación al cargar** (`ValidadorEscenario`): si un escenario no la pasa, el servidor no arranca y dice qué está mal.
- El mapa es rectangular, con el borde todo de muro, un reactor y de 2 a 4 salidas.
- Hay al menos 4 inicios de guardián.
- Las 4 vecinas del reactor son piso, y ninguna puerta queda pegada al reactor ni a una salida.
- Con todas las puertas cerradas, cada jugador llega al reactor; con todas abiertas, no queda ninguna zona encerrada.
- Las trampas al azar están entre 3 y 60.

El escenario se carga antes de iniciar (CA-23.3) y no puede cambiar durante la partida (CA-23.7). Un id desconocido
responde 400. No se puede pedir un escenario y obstáculos propios a la vez.

## 7. Valores configurables

Todos están en `ReglasArenaViva` (dentro de `ConfigPartida.ArenaViva`) y se validan al crear la partida. Las velocidades
(paso del guardián, pausa y ciclo de las puertas) son las mismas en todos los escenarios.

| Valor | Por defecto |
|---|---|
| `CantidadGuardianes` (en `ConfigPartida`) | 4 (rango 2–4) |
| `SegundosCongelamiento` (en `ConfigPartida`) | 3 |
| `PenalizacionTrampa` (en `ConfigPartida`) | 5 |
| `SegundosPorPasoGuardian` / `SegundosPausaGuardian` | 2 / 4 |
| `TrampasAleatorias` / `DistanciaMinimaEntreTrampas` | las del JSON del escenario (3 y 2 en el mapa clásico; rango 3–60 y 1–10) |
| `VecesEnergiaExtra` / `VecesSobrecarga` (por lote) | 6 / 2 (mínimos 3 y 2) |
| `SegundosCicloPuertas` | 6 |
| `PuntosEnergiaExtra` / `SegundosEnergiaExtra` | 25 / 8 |
| `SegundosSobrecarga` | 10 |
| `GuardianesInvasion` / `SegundosInvasion` | 5 / 30 |
| `SegundosPuntosDobles` / `MultiplicadorPuntosDobles` | 15 / 2 |
| `SegundosRutasAlternativas` | 15 |
| `CeldasLluviaEnergia` / `PuntosCeldaLluvia` / `SegundosLluviaEnergia` | 3 / 10 / 12 |
| `AlcanceArma` / `SegundosParalisisArma` / `SegundosRecargaArma` | 2 / 5 / 15 |

## 8. Mensajes al cliente

Detalle en `contratos-websocket.md`. Resumen: `match:state` trae `scenarioId`, `guardianCount`, `live` y
`players[].frozenSeconds`; `arena:update` (cada segundo), `event:started`, `event:ended`, `player:frozen` e
`item:collected`. `scenario.rows` sigue usando solo `W . S R T` (una puerta cerrada sale como `W` y una abierta como `.`).

## 9. Trazabilidad (criterio → pruebas)

Las pruebas están marcadas con `[Trait("CA", "…")]` (las del arma, con `[Trait("Requisito", "Arma paralizadora")]`); se pueden filtrar con
`dotnet test --filter "CA=22.3"`. CA-20.2 y la parte visual de CA-22.5 los completa Unity (Celeste): el servidor
envía el tipo de cada obstáculo, guardián, ítem y evento para que se dibujen distintos.

| Criterio | Pruebas |
|---|---|
| CA-20.1 | `TrampasYPuertasTests.DesdeFilas_LeePuertasYLugaresDeGuardian` |
| CA-20.2 | `TrampasYPuertasTests.ComoFilas_MuestraLaPuertaCerradaComoMuroYLaAbiertaComoPiso` |
| CA-20.3 | `TrampasYPuertasTests.Muro_SigueBloqueandoElPaso` |
| CA-20.4 | `EscenariosJsonTests.Iniciar_LasPuertasNoCambianTodasAlMismoTiempo`, `TrampasYPuertasTests.Puerta_Abierta_PermiteElPaso`, `TrampasYPuertasTests.Puerta_Cerrada_BloqueaElPaso`, `TrampasYPuertasTests.Puerta_ConArenaViva_AbreYCierraSolaCadaCiclo` |
| CA-20.5 | `ArenaVivaCasosDeUsoTests.Ejecutar_CaerEnUnaTrampa_RestaCincoCongelaYDevuelveLaNovedad`, `TrampasYPuertasTests.Trampa_ElJugadorQuedaInmovilizadoYLuegoRecuperaElMovimiento`, `TrampasYPuertasTests.Trampa_ElPuntajeNuncaBajaDeCero`, `TrampasYPuertasTests.Trampa_PorDefectoEsTierraMovediza_ComoAntes`, `TrampasYPuertasTests.Trampa_TodasLasVariantesRestanCincoPuntosYNoQuitanVida` |
| CA-20.6 | `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `ArenaVivaTiempoRealTests.Publicador_CaerEnTrampa_MandaPlayerFrozenATodos` |
| CA-20.7 | `ArenaVivaTiempoRealTests.Reloj_ConArenaViva_TraeLaFotoDeLaArenaCadaSegundo` |
| CA-20.8 | `EscenariosCatalogoTests.ArenaViva_JugadoresYGuardianesNuncaQuedanEnPosicionInvalidaNiSuperpuestos`, `TrampasYPuertasTests.Puerta_NoSeCierraSobreUnJugador` |
| CA-20.9 | `ArenaVivaCasosDeUsoTests.Iniciar_ActivaLaArenaVivaConCuatroGuardianesEnLosExtremosYLasTrampasDelEscenario`, `ArenaVivaCasosDeUsoTests.Iniciar_ConElMismoAzar_ColocaLasTrampasEnElMismoLugar`, `ArenaVivaCasosDeUsoTests.Iniciar_SinAzarInyectado_TambienActivaLaArenaViva`, `EscenariosJsonTests.Iniciar_ConOtraSemilla_LasTrampasCambianDeLugar`, `EscenariosJsonTests.Iniciar_EnLaEstacion_LasTrampasVanPrimeroALosCuellosDeBotella`, `EscenariosJsonTests.Iniciar_ReparteLasTrampasDelEscenarioSinPegarlasAlReactorNiALasSalidas`, `EscenariosJsonTests.Validador_TrampasFueraDeRango_Falla`, `TrampasYPuertasTests.Config_TrampasAleatoriasFueraDeRango_Falla`, `TrampasYPuertasTests.Iniciar_ConArenaViva_ColocaTresTrampasDistintas`, `TrampasYPuertasTests.Iniciar_ConLaMismaSemilla_ColocaLasMismasTrampas`, `TrampasYPuertasTests.Iniciar_ConUnaArenaSinEspacio_FallaSinEmpezarLaPartida`, `TrampasYPuertasTests.Iniciar_LasTrampasAleatoriasCaenSoloEnPisoLibre`, `TrampasYPuertasTests.Iniciar_LasTrampasCambianSegunLaSemilla`, `TrampasYPuertasTests.Iniciar_SinArenaViva_NoColocaTrampasNiGuardianes`, `TrampasYPuertasTests.Trampa_CeldaEmergente_CongelaDosSegundosMas`, `TrampasYPuertasTests.Trampa_RedInmovilizadora_CongelaUnSegundoMenos`, `TrampasYPuertasTests.Trampa_TierraMovediza_CongelaLoDeLaConfiguracionYRestaLaPenalizacion` |
| CA-21.1 | `ArenaVivaCasosDeUsoTests.Configurar_GuardianesFueraDeDosACuatro_Falla`, `ArenaVivaCasosDeUsoTests.Configurar_SinElegir_DejaCuatroGuardianes`, `ArenaVivaCasosDeUsoTests.Iniciar_ActivaLaArenaVivaConCuatroGuardianesEnLosExtremosYLasTrampasDelEscenario`, `ArenaVivaCasosDeUsoTests.Iniciar_CreaLosGuardianesElegidos`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `GuardianesTests.Config_GuardianesFueraDeDosACuatro_Falla`, `GuardianesTests.Config_PorDefectoHayCuatroGuardianes`, `GuardianesTests.Iniciar_ConCuatroGuardianes_EmpiezanEnLosCuatroExtremosDelEscenario`, `GuardianesTests.Iniciar_ConDosGuardianes_EmpiezanEnExtremosOpuestos`, `GuardianesTests.Iniciar_CreaLaCantidadDeGuardianesElegida`, `GuardianesTests.Iniciar_SinCeldasG_LosGuardianesEmpiezanCercaDeLosBordes` |
| CA-21.2 | `GuardianesTests.Guardian_BloqueaElPasoDeLosJugadores`, `GuardianesTests.Guardian_DaUnPasoCadaDosSegundosHaciaElJugadorMasCercano`, `GuardianesTests.Guardian_NoAtraviesaMuros` |
| CA-21.3 | `GuardianesTests.Guardian_AlAlcanzarAUnJugadorLoCongelaYSeQuedaQuieto` |
| CA-21.4 | `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones` |
| CA-21.5 | `ArenaVivaCasosDeUsoTests.Ejecutar_EstandoCongelado_NoSeMueve`, `ArmaParalizadoraTests.Congelado_NoPuedeDisparar`, `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones` |
| CA-21.6 | `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones`, `TrampasYPuertasTests.Trampa_ElJugadorQuedaInmovilizadoYLuegoRecuperaElMovimiento` |
| CA-21.7 | `ArenaVivaCasosDeUsoTests.Ejecutar_CaerEnUnaTrampa_RestaCincoCongelaYDevuelveLaNovedad`, `ArenaVivaTiempoRealTests.AMensaje_Congelamiento_EsPlayerFrozenConOrigenYTrampa`, `ArenaVivaTiempoRealTests.Publicador_CaerEnTrampa_MandaPlayerFrozenATodos`, `ArenaVivaTiempoRealTests.Reloj_CuandoUnGuardianAlcanzaAUnJugador_LoTraeEnLasNovedades`, `GuardianesTests.Guardian_AlAlcanzarARegistraUnaNovedadParaTodos`, `TrampasYPuertasTests.Trampa_RegistraUnaNovedadDeCongelamientoConLaVariante` |
| CA-21.8 | `GuardianesTests.Jugador_NoPuedeCongelarseNiDescongelarseDesdeFueraDelDominio` |
| CA-21.9 | `GuardianesTests.Guardian_AlcanzarAUnJugadorNoCambiaSuPuntaje` |
| CA-21.10 | `GuardianesTests.Guardian_AlAlcanzarAUnJugadorYaInmovilizado_LeSumaTiempo`, `GuardianesTests.Guardian_DespuesDeAlcanzarAUnJugador_NoLoCongelaDeNuevoMientrasEstaQuieto` |
| CA-21.11 | `GuardianesTests.Guardian_SiElMasCercanoEstaCongelado_VaPorOtroJugador`, `GuardianesTests.Guardian_SiTodosEstanCongelados_NoSeMueve` |
| CA-22.1 | `ArenaVivaTiempoRealTests.AMensaje_EnergiaRecogida_EsItemCollected`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EscenariosJsonTests.EnergiaAdicional_AparecePorLoMenosSeisVecesEnLosPuntosDelEscenario`, `EventosEspecialesTests.EnergiaExtra_AparecePisandoUnaCeldaLibreDeLaArena`, `EventosEspecialesTests.EnergiaExtra_DaVeinticincoPuntosYDuraOchoSegundos`, `EventosEspecialesTests.EnergiaExtra_ElPrimeroQueLaPisaSeLlevaLosPuntos`, `EventosEspecialesTests.EnergiaExtra_NoLaPuedenTomarDosJugadores`, `EventosEspecialesTests.EnergiaExtra_SiNadieLaTomaDesapareceConElEvento`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas` |
| CA-22.2 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.Invasion_AparecenCincoGuardianesMasYDespuesDesaparecen`, `EventosEspecialesTests.Invasion_LosInvasoresPersiguenComoLosDemasGuardianes` |
| CA-22.3 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.Calendario_NoEmpiezaEnElPrimerSegundoYNoSeAmontona`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas`, `EventosEspecialesTests.Sobrecarga_DesactivaElReactorYDespuesVuelveAFuncionar`, `EventosEspecialesTests.Sobrecarga_DuraDiezSegundosYOcurreAlMenosDosVecesEnMomentosDistintos`, `EventosEspecialesTests.Sobrecarga_ElMomentoCambiaSegunElAzar`, `EventosEspecialesTests.Sobrecarga_LaPrimeraCaeEntreLos20YLos45SegundosYLaOtraEnLaSegundaMitad` |
| CA-22.4 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.LluviaDeEnergia_CreaVariasCeldasQueTerminanAlTomarLaUltima`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas`, `EventosEspecialesTests.Partida_SinPuertas_UsaLluviaDeEnergiaComoSegundoCreativo`, `EventosEspecialesTests.PuntosDobles_LaExtraccionVale_ElDobleSoloMientrasDura`, `EventosEspecialesTests.PuntosDobles_NoDuplicaLasPenalizaciones`, `EventosEspecialesTests.RutasAlternativas_DejanTodasLasPuertasAbiertasMientrasDuran` |
| CA-22.5 | `ArenaVivaTiempoRealTests.AMensaje_Eventos_LlevanTipoDuracionYUnTextoParaMostrar`, `ArenaVivaTiempoRealTests.Reloj_CuandoEmpiezaUnEvento_LoTraeEnLasNovedadesUnaSolaVez`, `EventosEspecialesTests.CadaEventoQueEmpiezaRegistraUnaNovedadConSuDuracion` |
| CA-22.6 | `ArenaVivaTiempoRealTests.Publicador_EnergiaRecogida_MandaMovimientoPuntajeYLuegoItemCollected` |
| CA-22.7 | `EventosEspecialesTests.Evento_AlTerminarRestauraLasCondicionesAnteriores`, `EventosEspecialesTests.Sobrecarga_DuraLoQueDiceLaConfiguracionYElEventoTerminaConElla` |
| CA-22.8 | `EventosEspecialesTests.PartidaTerminada_NoAceptaRecogerEnergia`, `EventosEspecialesTests.PartidaTerminada_NoTieneEventosNiEnergiaEnLaArenaYElTiempoNoLosMueve` |
| CA-22.9 | `EventosEspecialesTests.Evento_NoSeRepiteSinoHayPartidaNueva_SoloSeActivaUnaVezLaArenaViva`, `EventosEspecialesTests.Evento_NuncaEstaActivoDosVecesALaVez` |
| CA-22.10 | `EventosEspecialesTests.Calendario_ConLaMismaSemilla_EsElMismo`, `EventosEspecialesTests.Reglas_ConValoresInvalidos_Fallan`, `EventosEspecialesTests.Reglas_ValoresPorDefecto_SonLosAcordados` |
| CA-23.1 | `ArenaVivaCasosDeUsoTests.ListarEscenarios_DevuelveLosTresConLoNecesarioParaElegir`, `EscenariosCatalogoTests.Catalogo_TieneAlMenosDosEscenariosDistintos`, `EscenariosJsonTests.Catalogo_SeCargaDeLosJsonEnSuOrden`, `EscenariosJsonTests.Escenarios_TienenDistribucionesDistintas` |
| CA-23.2 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenarioDesconocido_FallaYNoCreaLaPartida`, `EscenariosCatalogoTests.Escenario_SeCargaYDefineLimitesInicioReactorObstaculosYGuardianes`, `EscenariosJsonTests.Escenario_PasaTodasLasValidaciones`, `EscenariosJsonTests.Validador_BordeAbiertoOPocosGuardianes_Falla` |
| CA-23.3 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenario_CargaEseMapaAntesDeEmpezar`, `ArenaVivaCasosDeUsoTests.Configurar_GuardaElIdDelCatalogoAunqueVengaConMayusculas`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EscenariosCatalogoTests.Obtener_BuscaPorIdSinImportarMayusculas` |
| CA-23.4 | `ArenaVivaCasosDeUsoTests.Obtener_TodosVenLaMismaArenaViva`, `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `EscenariosCatalogoTests.Partida_TodosLosJugadoresVenElMismoEscenario` |
| CA-23.5 | `EscenariosCatalogoTests.Escenario_LosInicioNoEstanEnObstaculosNiFueraDeLaArena`, `EscenariosCatalogoTests.Partida_LosJugadoresAparecenEnLosInicioDelEscenario`, `EscenariosJsonTests.Escenario_PasaTodasLasValidaciones`, `EscenariosJsonTests.Validador_PuertaPegadaAlReactor_Falla`, `EscenariosJsonTests.Validador_ReactorSoloAlcanzablePorPuertas_Falla` |
| CA-23.6 | `EscenariosCatalogoTests.Escenario_ElReactorSeAlcanzaSinDependerDeLasPuertas`, `EscenariosCatalogoTests.Escenario_ElReactorTieneSusCuatroVecinasLibresYTodoSeAlcanzaDesdeLosInicios` |
| CA-23.7 | `ArenaVivaCasosDeUsoTests.Iniciar_DosVeces_FallaYElEscenarioNoCambia`, `EscenariosCatalogoTests.Partida_ElEscenarioNoCambiaDuranteLaPartida` |
| CA-23.8 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenarioYObstaculosALaVez_Falla`, `ArenaVivaCasosDeUsoTests.Configurar_SinEscenario_UsaElMapaClasicoComoAntes`, `EscenariosCatalogoTests.Obtener_SinIdDevuelveElMapaClasicoYUnoDesconocidoFalla`, `EscenariosJsonTests.Aplicar_PoneElIdYLasTrampasDelEscenarioSinTocarElResto` |
| Arma paralizadora (sin CA) | `ArenaVivaTiempoRealTests.AMensaje_ArmaDisparada_EsWeaponFiredConLosGuardianesParalizados`, `ArenaVivaTiempoRealTests.EstadoPartida_MuestraRecargaDelJugadorYGuardianesParalizados`, `ArenaVivaTiempoRealTests.Publicador_ArmaRecargando_RechazaSoloAlJugador`, `ArenaVivaTiempoRealTests.Publicador_Disparo_MandaWeaponFiredATodosSinMoverAlJugador`, `ArenaVivaTiempoRealTests.Rechazo_ArmaRecargando_TieneSuCodigo`, `ArmaParalizadoraTests.Accion_LejosDelReactor_DisparaYParalizaAlGuardianCercano`, `ArmaParalizadoraTests.Congelado_NoPuedeDisparar`, `ArmaParalizadoraTests.DisparoVacio_SeGastaIgualYEntraEnRecarga`, `ArmaParalizadoraTests.Disparo_NoAfectaJugadoresNiPuntajes`, `ArmaParalizadoraTests.DosDisparos_LaParalisisNoSeAcumula`, `ArmaParalizadoraTests.GuardianParalizado_NoSeMueveNiCongelaYDespuesVuelveAPerseguir`, `ArmaParalizadoraTests.JugadorJuntoAUnGuardianParalizado_NoQuedaCongelado`, `ArmaParalizadoraTests.JuntoAlReactor_AccionSigueExtrayendo`, `ArmaParalizadoraTests.Recarga_EsDeCadaJugador`, `ArmaParalizadoraTests.Recargando_NoDisparaHastaQuePasenQuinceSegundos`, `ArmaParalizadoraTests.Reglas_ValoresPorDefectoYValidacion`, `ArmaParalizadoraTests.SinArenaViva_LejosDelReactorSigueRechazandoComoAntes` |
