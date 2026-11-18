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
| Puerta automática | `D` | Cambia de estado cada **6 s**. Cerrada = muro; abierta = piso. Al iniciar, las puertas se alternan (la 1.ª cerrada, la 2.ª abierta, …). **Nunca se cierra con alguien encima**: espera a que la celda quede libre y lo vuelve a intentar cada segundo (CA-20.8). |
| Trampa fija | `T` | La que viene dibujada en el mapa. Es tierra movediza. |
| Trampa aleatoria | — | Al iniciar, el servidor coloca **3** trampas al azar, **una de cada variante** (CA-20.9). |

**Variantes de trampa.** Todas inmovilizan y restan 5 puntos; cambia cuánto inmovilizan.

| Variante | Código WebSocket | Inmoviliza |
|---|---|---|
| Tierra movediza | `QUICKSAND` | 3 s (`Config.SegundosCongelamiento`) |
| Celda emergente (surge del piso) | `RISING_CELL` | 5 s (+2) |
| Red inmovilizadora | `NET` | 2 s (−1) |

**Dónde caen las aleatorias:** en piso libre, a 3 o más celdas de todos los inicios de jugador, a 2 o más del reactor
y de los inicios de guardián, nunca sobre una puerta, un muro u otra trampa. Si el mapa no tiene lugar, la partida
no inicia (error) y queda en espera.

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

## 5. Eventos especiales (US-22)

**Activación.** Al iniciar se arma un calendario con un lote de 8 eventos en orden aleatorio, repartidos a lo largo de la
duración de la partida: el evento *i* cae cerca del segundo `hueco × (i+1)` (con `hueco = duración / 9`, unos 20 s en
una partida de 3 minutos), corrido al azar hasta ±¼ de hueco, con al menos 2 s entre uno y otro y nunca en el primer
segundo. Cuando el lote se acaba (prórroga por empate o modo por puntos) se programa otro igual.
**Sin repeticiones accidentales** (CA-22.9): un evento del mismo tipo nunca está activo dos veces a la vez; si le toca
empezar y ya está activo, se pospone 2 s.

| Evento | Código | Veces por lote | Duración | Efecto | Al terminar |
|---|---|---|---|---|---|
| Energía adicional | `EXTRA_ENERGY` | 3 | 8 s | Aparece un ícono en una celda libre al azar. El primero que lo pisa gana **+25**. | Si nadie lo tomó, desaparece. Si alguien lo tomó, el evento termina en ese momento. |
| Sobrecarga del reactor | `REACTOR_OVERLOAD` | 2 | 10 s | Se llama `Partida.DesactivarReactor(10)`: nadie puede extraer (`REACTOR_DESACTIVADO`). | El reactor vuelve a funcionar. |
| Invasión de guardianes | `GUARDIAN_INVASION` | 1 | 30 s | Aparecen **5 guardianes más**, iguales a los normales, lejos de los jugadores y del reactor. | Los 5 temporales desaparecen. |
| Puntos dobles *(creativo 1)* | `DOUBLE_POINTS` | 1 | 15 s | Extraer y recoger energía valen ×2. Las penalizaciones no se duplican. | Todo vuelve a valer lo normal. |
| Rutas alternativas *(creativo 2)* | `ALTERNATE_ROUTES` | 1 | 15 s | Todas las puertas se abren al instante y quedan abiertas. | Las puertas retoman su ciclo de 6 s. |
| Lluvia de energía *(creativo 2, solo arenas sin puertas)* | `ENERGY_RAIN` | 1 | 12 s | Aparecen 3 celdas de energía de +10 cada una. | Las que queden desaparecen (también termina si se toman todas). |

Cada inicio se avisa con `event:started` (tipo, duración y un texto listo para mostrar, CA-22.5) y cada fin con
`event:ended` (CA-22.7). Los eventos activos viajan también en `arena:update` (CA-22.6).

## 6. Escenarios (US-23)

Se eligen al crear la partida (`escenarioId`); la lista sale de `GET /api/escenarios`. Todos miden 15×11, tienen
4 inicios de jugador en las esquinas, 4 inicios de guardián y el reactor al centro con sus 4 vecinas libres. Las reglas
son las mismas; cambian los muros, las puertas y las trampas fijas. Con las puertas cerradas, todos los jugadores pueden
llegar al reactor (nadie queda encerrado).

| Id | Nombre | Distribución |
|---|---|---|
| `laboratorio` | Laboratorio subterráneo | Corredores laterales y una sala central con 8 puertas automáticas. 4 trampas fijas. |
| `estacion` | Estación espacial | Pilares alrededor del reactor y 2 puertas en el anillo central. 4 trampas fijas. |
| `militar` | Complejo militar futurista | Barreras con pasillos de seguridad, 4 puertas y 2 trampas junto al reactor. |
| *(sin id)* | Arena clásica | El mapa que ya existía, sin puertas. Ahí «Rutas alternativas» se reemplaza por «Lluvia de energía». |

El escenario se carga antes de iniciar (CA-23.3) y no puede cambiar durante la partida (CA-23.7). Un id desconocido
responde 400. No se puede pedir un escenario y obstáculos propios a la vez.

## 7. Valores configurables

Todos están en `ReglasArenaViva` (dentro de `ConfigPartida.ArenaViva`) y se validan al crear la partida.

| Valor | Por defecto |
|---|---|
| `CantidadGuardianes` (en `ConfigPartida`) | 4 (rango 2–4) |
| `SegundosCongelamiento` (en `ConfigPartida`) | 3 |
| `PenalizacionTrampa` (en `ConfigPartida`) | 5 |
| `SegundosPorPasoGuardian` / `SegundosPausaGuardian` | 2 / 4 |
| `TrampasAleatorias` | 3 (rango 3–9) |
| `SegundosCicloPuertas` | 6 |
| `PuntosEnergiaExtra` / `SegundosEnergiaExtra` | 25 / 8 |
| `SegundosSobrecarga` | 10 |
| `GuardianesInvasion` / `SegundosInvasion` | 5 / 30 |
| `SegundosPuntosDobles` / `MultiplicadorPuntosDobles` | 15 / 2 |
| `SegundosRutasAlternativas` | 15 |
| `CeldasLluviaEnergia` / `PuntosCeldaLluvia` / `SegundosLluviaEnergia` | 3 / 10 / 12 |

## 8. Mensajes al cliente

Detalle en `contratos-websocket.md`. Resumen: `match:state` trae `scenarioId`, `guardianCount`, `live` y
`players[].frozenSeconds`; `arena:update` (cada segundo), `event:started`, `event:ended`, `player:frozen` e
`item:collected`. `scenario.rows` sigue usando solo `W . S R T` (una puerta cerrada sale como `W` y una abierta como `.`).

## 9. Trazabilidad (criterio → pruebas)

Las pruebas están marcadas con `[Trait("CA", "…")]`; se pueden filtrar con
`dotnet test --filter "CA=22.3"`. CA-20.2 y la parte visual de CA-22.5 los completa Unity (Celeste): el servidor
envía el tipo de cada obstáculo, guardián, ítem y evento para que se dibujen distintos.

| Criterio | Pruebas |
|---|---|
| CA-20.1 | `TrampasYPuertasTests.DesdeFilas_LeePuertasYLugaresDeGuardian` |
| CA-20.2 | `TrampasYPuertasTests.ComoFilas_MuestraLaPuertaCerradaComoMuroYLaAbiertaComoPiso` |
| CA-20.3 | `TrampasYPuertasTests.Muro_SigueBloqueandoElPaso` |
| CA-20.4 | `TrampasYPuertasTests.Puerta_Abierta_PermiteElPaso`, `TrampasYPuertasTests.Puerta_Cerrada_BloqueaElPaso`, `TrampasYPuertasTests.Puerta_ConArenaViva_AbreYCierraSolaCadaCiclo` |
| CA-20.5 | `ArenaVivaCasosDeUsoTests.Ejecutar_CaerEnUnaTrampa_RestaCincoCongelaYDevuelveLaNovedad`, `TrampasYPuertasTests.Trampa_ElJugadorQuedaInmovilizadoYLuegoRecuperaElMovimiento`, `TrampasYPuertasTests.Trampa_ElPuntajeNuncaBajaDeCero`, `TrampasYPuertasTests.Trampa_PorDefectoEsTierraMovediza_ComoAntes`, `TrampasYPuertasTests.Trampa_TodasLasVariantesRestanCincoPuntosYNoQuitanVida` |
| CA-20.6 | `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `ArenaVivaTiempoRealTests.Publicador_CaerEnTrampa_MandaPlayerFrozenATodos` |
| CA-20.7 | `ArenaVivaTiempoRealTests.Reloj_ConArenaViva_TraeLaFotoDeLaArenaCadaSegundo` |
| CA-20.8 | `EscenariosCatalogoTests.ArenaViva_JugadoresYGuardianesNuncaQuedanEnPosicionInvalidaNiSuperpuestos`, `TrampasYPuertasTests.Puerta_NoSeCierraSobreUnJugador` |
| CA-20.9 | `ArenaVivaCasosDeUsoTests.Iniciar_ActivaLaArenaVivaConCuatroGuardianesEnLosExtremosYTresTrampasDistintas`, `ArenaVivaCasosDeUsoTests.Iniciar_ConElMismoAzar_ColocaLasTrampasEnElMismoLugar`, `ArenaVivaCasosDeUsoTests.Iniciar_SinAzarInyectado_TambienActivaLaArenaViva`, `TrampasYPuertasTests.Config_TrampasAleatoriasFueraDeRango_Falla`, `TrampasYPuertasTests.Iniciar_ConArenaViva_ColocaTresTrampasDistintas`, `TrampasYPuertasTests.Iniciar_ConLaMismaSemilla_ColocaLasMismasTrampas`, `TrampasYPuertasTests.Iniciar_ConUnaArenaSinEspacio_FallaSinEmpezarLaPartida`, `TrampasYPuertasTests.Iniciar_LasTrampasAleatoriasCaenSoloEnPisoLibre`, `TrampasYPuertasTests.Iniciar_LasTrampasCambianSegunLaSemilla`, `TrampasYPuertasTests.Iniciar_SinArenaViva_NoColocaTrampasNiGuardianes`, `TrampasYPuertasTests.Trampa_CeldaEmergente_CongelaDosSegundosMas`, `TrampasYPuertasTests.Trampa_RedInmovilizadora_CongelaUnSegundoMenos`, `TrampasYPuertasTests.Trampa_TierraMovediza_CongelaLoDeLaConfiguracionYRestaLaPenalizacion` |
| CA-21.1 | `ArenaVivaCasosDeUsoTests.Configurar_GuardianesFueraDeDosACuatro_Falla`, `ArenaVivaCasosDeUsoTests.Configurar_SinElegir_DejaCuatroGuardianes`, `ArenaVivaCasosDeUsoTests.Iniciar_ActivaLaArenaVivaConCuatroGuardianesEnLosExtremosYTresTrampasDistintas`, `ArenaVivaCasosDeUsoTests.Iniciar_CreaLosGuardianesElegidos`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `GuardianesTests.Config_GuardianesFueraDeDosACuatro_Falla`, `GuardianesTests.Config_PorDefectoHayCuatroGuardianes`, `GuardianesTests.Iniciar_ConCuatroGuardianes_EmpiezanEnLosCuatroExtremosDelEscenario`, `GuardianesTests.Iniciar_ConDosGuardianes_EmpiezanEnExtremosOpuestos`, `GuardianesTests.Iniciar_CreaLaCantidadDeGuardianesElegida`, `GuardianesTests.Iniciar_SinCeldasG_LosGuardianesEmpiezanCercaDeLosBordes` |
| CA-21.2 | `GuardianesTests.Guardian_BloqueaElPasoDeLosJugadores`, `GuardianesTests.Guardian_DaUnPasoCadaDosSegundosHaciaElJugadorMasCercano`, `GuardianesTests.Guardian_NoAtraviesaMuros` |
| CA-21.3 | `GuardianesTests.Guardian_AlAlcanzarAUnJugadorLoCongelaYSeQuedaQuieto` |
| CA-21.4 | `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones` |
| CA-21.5 | `ArenaVivaCasosDeUsoTests.Ejecutar_EstandoCongelado_NoSeMueve`, `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones` |
| CA-21.6 | `GuardianesTests.Congelado_NoPuedeActuarYLuegoRecuperaLasAcciones`, `TrampasYPuertasTests.Trampa_ElJugadorQuedaInmovilizadoYLuegoRecuperaElMovimiento` |
| CA-21.7 | `ArenaVivaCasosDeUsoTests.Ejecutar_CaerEnUnaTrampa_RestaCincoCongelaYDevuelveLaNovedad`, `ArenaVivaTiempoRealTests.AMensaje_Congelamiento_EsPlayerFrozenConOrigenYTrampa`, `ArenaVivaTiempoRealTests.Publicador_CaerEnTrampa_MandaPlayerFrozenATodos`, `ArenaVivaTiempoRealTests.Reloj_CuandoUnGuardianAlcanzaAUnJugador_LoTraeEnLasNovedades`, `GuardianesTests.Guardian_AlAlcanzarARegistraUnaNovedadParaTodos`, `TrampasYPuertasTests.Trampa_RegistraUnaNovedadDeCongelamientoConLaVariante` |
| CA-21.8 | `GuardianesTests.Jugador_NoPuedeCongelarseNiDescongelarseDesdeFueraDelDominio` |
| CA-21.9 | `GuardianesTests.Guardian_AlcanzarAUnJugadorNoCambiaSuPuntaje` |
| CA-21.10 | `GuardianesTests.Guardian_AlAlcanzarAUnJugadorYaInmovilizado_LeSumaTiempo`, `GuardianesTests.Guardian_DespuesDeAlcanzarAUnJugador_NoLoCongelaDeNuevoMientrasEstaQuieto` |
| CA-21.11 | `GuardianesTests.Guardian_SiElMasCercanoEstaCongelado_VaPorOtroJugador`, `GuardianesTests.Guardian_SiTodosEstanCongelados_NoSeMueve` |
| CA-22.1 | `ArenaVivaTiempoRealTests.AMensaje_EnergiaRecogida_EsItemCollected`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.EnergiaExtra_AparecePisandoUnaCeldaLibreDeLaArena`, `EventosEspecialesTests.EnergiaExtra_DaVeinticincoPuntosYDuraOchoSegundos`, `EventosEspecialesTests.EnergiaExtra_ElPrimeroQueLaPisaSeLlevaLosPuntos`, `EventosEspecialesTests.EnergiaExtra_NoLaPuedenTomarDosJugadores`, `EventosEspecialesTests.EnergiaExtra_SiNadieLaTomaDesapareceConElEvento`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas` |
| CA-22.2 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.Invasion_AparecenCincoGuardianesMasYDespuesDesaparecen`, `EventosEspecialesTests.Invasion_LosInvasoresPersiguenComoLosDemasGuardianes` |
| CA-22.3 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.Calendario_NoEmpiezaEnElPrimerSegundoYNoSeAmontona`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas`, `EventosEspecialesTests.Sobrecarga_DesactivaElReactorYDespuesVuelveAFuncionar`, `EventosEspecialesTests.Sobrecarga_DuraDiezSegundosYOcurreAlMenosDosVecesEnMomentosDistintos`, `EventosEspecialesTests.Sobrecarga_ElMomentoCambiaSegunElAzar` |
| CA-22.4 | `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EventosEspecialesTests.LluviaDeEnergia_CreaVariasCeldasQueTerminanAlTomarLaUltima`, `EventosEspecialesTests.Partida_ConPuertas_TieneTodosLosEventosLasVecesPedidas`, `EventosEspecialesTests.Partida_SinPuertas_UsaLluviaDeEnergiaComoSegundoCreativo`, `EventosEspecialesTests.PuntosDobles_LaExtraccionVale_ElDobleSoloMientrasDura`, `EventosEspecialesTests.PuntosDobles_NoDuplicaLasPenalizaciones`, `EventosEspecialesTests.RutasAlternativas_DejanTodasLasPuertasAbiertasMientrasDuran` |
| CA-22.5 | `ArenaVivaTiempoRealTests.AMensaje_Eventos_LlevanTipoDuracionYUnTextoParaMostrar`, `ArenaVivaTiempoRealTests.Reloj_CuandoEmpiezaUnEvento_LoTraeEnLasNovedadesUnaSolaVez`, `EventosEspecialesTests.CadaEventoQueEmpiezaRegistraUnaNovedadConSuDuracion` |
| CA-22.6 | `ArenaVivaTiempoRealTests.Publicador_EnergiaRecogida_MandaMovimientoPuntajeYLuegoItemCollected` |
| CA-22.7 | `EventosEspecialesTests.Evento_AlTerminarRestauraLasCondicionesAnteriores`, `EventosEspecialesTests.Sobrecarga_DuraLoQueDiceLaConfiguracionYElEventoTerminaConElla` |
| CA-22.8 | `EventosEspecialesTests.PartidaTerminada_NoAceptaRecogerEnergia`, `EventosEspecialesTests.PartidaTerminada_NoTieneEventosNiEnergiaEnLaArenaYElTiempoNoLosMueve` |
| CA-22.9 | `EventosEspecialesTests.Evento_NoSeRepiteSinoHayPartidaNueva_SoloSeActivaUnaVezLaArenaViva`, `EventosEspecialesTests.Evento_NuncaEstaActivoDosVecesALaVez` |
| CA-22.10 | `EventosEspecialesTests.Calendario_ConLaMismaSemilla_EsElMismo`, `EventosEspecialesTests.Reglas_ConValoresInvalidos_Fallan`, `EventosEspecialesTests.Reglas_ValoresPorDefecto_SonLosAcordados` |
| CA-23.1 | `ArenaVivaCasosDeUsoTests.ListarEscenarios_DevuelveLosTresConLoNecesarioParaElegir`, `EscenariosCatalogoTests.Catalogo_TieneAlMenosDosEscenariosDistintos` |
| CA-23.2 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenarioDesconocido_FallaYNoCreaLaPartida`, `EscenariosCatalogoTests.Escenario_SeCargaYDefineLimitesInicioReactorObstaculosYGuardianes` |
| CA-23.3 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenario_CargaEseMapaAntesDeEmpezar`, `ArenaVivaCasosDeUsoTests.Configurar_GuardaElIdDelCatalogoAunqueVengaConMayusculas`, `ArenaVivaTiempoRealTests.Aceptacion_PartidaDeTresMinutos_TieneTodosLosElementosDeLaArenaViva`, `EscenariosCatalogoTests.Obtener_BuscaPorIdSinImportarMayusculas` |
| CA-23.4 | `ArenaVivaCasosDeUsoTests.Obtener_TodosVenLaMismaArenaViva`, `ArenaVivaTiempoRealTests.EstadoPartida_ConArenaViva_IncluyeEscenarioLiveYCongelados`, `EscenariosCatalogoTests.Partida_TodosLosJugadoresVenElMismoEscenario` |
| CA-23.5 | `EscenariosCatalogoTests.Escenario_LosInicioNoEstanEnObstaculosNiFueraDeLaArena`, `EscenariosCatalogoTests.Partida_LosJugadoresAparecenEnLosInicioDelEscenario` |
| CA-23.6 | `EscenariosCatalogoTests.Escenario_ElReactorSeAlcanzaSinDependerDeLasPuertas`, `EscenariosCatalogoTests.Escenario_ElReactorTieneSusCuatroVecinasLibresYTodoSeAlcanzaDesdeLosInicios` |
| CA-23.7 | `ArenaVivaCasosDeUsoTests.Iniciar_DosVeces_FallaYElEscenarioNoCambia`, `EscenariosCatalogoTests.Partida_ElEscenarioNoCambiaDuranteLaPartida` |
| CA-23.8 | `ArenaVivaCasosDeUsoTests.Configurar_ConEscenarioYObstaculosALaVez_Falla`, `ArenaVivaCasosDeUsoTests.Configurar_SinEscenario_UsaElMapaClasicoComoAntes`, `EscenariosCatalogoTests.Obtener_SinIdDevuelveElMapaClasicoYUnoDesconocidoFalla` |
