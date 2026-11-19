"use strict";

// US-30: cliente móvil ligero. Usa el mismo servidor que Unity:
//  - REST para login (normal y GitHub), perfil, ranking, historial y "¿en qué partida estoy?";
//  - WebSocket /ws/partida con "match:watch" para mirar la partida en vivo SIN unirse
//    (entra como espectador y no le quita la conexión a Unity).

const CLAVE_SESION = "reactor.sesion";
const estado = { token: null, usuario: null, socket: null, partida: null, github: null, criterio: "victorias" };
const $ = (id) => document.getElementById(id);

// ---------- Utilidades ----------

function escapar(texto) {
    return String(texto ?? "").replace(/[&<>"']/g, (c) =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

function mostrarError(id, texto) {
    $(id).textContent = texto || "";
}

function mmss(segundos) {
    const s = Math.max(0, Math.round(segundos));
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}

/** Círculo con la inicial; si el avatar tiene imagen en el servidor, la pone encima. */
function avatar(id, nombre, grande = false) {
    const inicial = escapar((nombre || "?").charAt(0).toUpperCase());
    const imagen = id ? `<img src="/api/avatares/${escapar(id)}/imagen" alt="" onerror="this.remove()">` : "";
    return `<span class="avatar${grande ? " grande" : ""}">${inicial}${imagen}</span>`;
}

/** Llama a la API con el token. Devuelve el JSON, null si es 204, o lanza un Error con el mensaje del servidor. */
async function api(ruta, opciones = {}) {
    const encabezados = { "Content-Type": "application/json" };
    if (estado.token) encabezados.Authorization = `Bearer ${estado.token}`;

    const respuesta = await fetch(ruta, { ...opciones, headers: encabezados });

    if (respuesta.status === 401 && estado.token) {
        cerrarSesion("Tu sesión venció. Volvé a entrar.");
        throw new Error("Tu sesión venció.");
    }
    if (respuesta.status === 204) return null;

    const datos = await respuesta.json().catch(() => null);
    if (!respuesta.ok) throw new Error(datos?.mensaje || datos?.error || `Error ${respuesta.status}`);
    return datos;
}

// ---------- Sesión (se guarda en el navegador para no pedir login cada vez) ----------

function guardarSesion() {
    try { localStorage.setItem(CLAVE_SESION, JSON.stringify({ token: estado.token, usuario: estado.usuario })); } catch { /* sin almacenamiento */ }
}

function leerSesion() {
    try { return JSON.parse(localStorage.getItem(CLAVE_SESION)); } catch { return null; }
}

function borrarSesion() {
    try { localStorage.removeItem(CLAVE_SESION); } catch { /* sin almacenamiento */ }
}

function iniciarSesion(login) {
    estado.token = login.accessToken;
    estado.usuario = login.user;
    guardarSesion();
    $("login-clave").value = "";
    mostrarApp();
}

function cerrarSesion(mensaje = "") {
    cerrarSocket();
    terminarGitHub();
    estado.token = null;
    estado.usuario = null;
    borrarSesion();
    mostrarLogin(mensaje);
}

function mostrarLogin(mensaje = "") {
    $("pantalla-app").hidden = true;
    $("pantalla-login").hidden = false;
    $("btn-salir").hidden = true;
    mostrarError("login-error", mensaje);
}

function mostrarApp() {
    $("pantalla-login").hidden = true;
    $("pantalla-app").hidden = false;
    $("btn-salir").hidden = false;
    cambiarSeccion("perfil");
}

// ---------- Login normal ----------

async function entrar(evento) {
    evento.preventDefault();
    mostrarError("login-error", "");
    try {
        const login = await api("/api/auth/login", {
            method: "POST",
            body: JSON.stringify({ usernameOrEmail: $("login-usuario").value.trim(), password: $("login-clave").value }),
        });
        iniciarSesion(login);
    } catch (error) {
        mostrarError("login-error", error.message);
    }
}

// ---------- Login con GitHub (US-29, device flow) ----------

async function iniciarGitHub() {
    mostrarError("login-error", "");
    try {
        const inicio = await api("/api/auth/github/iniciar", { method: "POST" });
        $("github-link").href = inicio.verificationUri;
        $("github-codigo").textContent = inicio.userCode;
        $("github-paso").hidden = false;
        $("btn-github").disabled = true;
        estado.github = { deviceCode: inicio.deviceCode, intervalo: inicio.interval, temporizador: null, consultando: false };
        programarConsultaGitHub();
    } catch (error) {
        mostrarError("login-error", error.message);
    }
}

function programarConsultaGitHub() {
    const g = estado.github;
    if (g) g.temporizador = setTimeout(consultarGitHub, g.intervalo * 1000);
}

async function consultarGitHub() {
    const g = estado.github;
    if (!g || g.consultando) return;
    g.consultando = true;
    try {
        const r = await api("/api/auth/github/consultar", {
            method: "POST",
            body: JSON.stringify({ deviceCode: g.deviceCode }),
        });
        if (estado.github !== g) return;            // lo cancelaron mientras esperaba
        if (r.estado === "COMPLETADO") {
            terminarGitHub();
            iniciarSesion(r.login);
            return;
        }
        if (r.interval) g.intervalo = r.interval;     // GitHub pidió ir más lento
        g.consultando = false;
        programarConsultaGitHub();
    } catch (error) {
        terminarGitHub();
        mostrarError("login-error", error.message);   // cancelado, expirado, sin correo...
    }
}

function terminarGitHub() {
    if (estado.github?.temporizador) clearTimeout(estado.github.temporizador);
    estado.github = null;
    $("github-paso").hidden = true;
    $("btn-github").disabled = false;
}

// Al volver de la pestaña de GitHub, consulta enseguida (el celular frena los temporizadores en segundo plano).
document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible" && estado.github) {
        clearTimeout(estado.github.temporizador);
        consultarGitHub();
    }
});

// ---------- Secciones ----------

function cambiarSeccion(nombre) {
    document.querySelectorAll("[data-seccion]").forEach((b) => b.classList.toggle("activa", b.dataset.seccion === nombre));
    for (const s of ["perfil", "ranking", "historial", "vivo"]) $(`seccion-${s}`).hidden = s !== nombre;
    mostrarError("app-error", "");
    if (nombre !== "vivo") cerrarSocket();

    const cargar = { perfil: cargarPerfil, ranking: cargarRanking, historial: cargarHistorial, vivo: cargarVivo }[nombre];
    cargar().catch((error) => mostrarError("app-error", error.message));
}

// ---------- Perfil ----------

const dato = (etiqueta, valor) =>
    `<div class="dato"><span class="valor">${escapar(valor)}</span><span class="pequeno">${etiqueta}</span></div>`;

async function cargarPerfil() {
    const p = await api("/api/perfil");
    estado.usuario = { ...estado.usuario, id: p.id, username: p.username };
    const e = p.estadisticas;

    $("seccion-perfil").innerHTML = `
    <div class="tarjeta perfil">
      ${avatar(p.avatarId, p.username, true)}
      <div>
        <h2>${escapar(p.username)}</h2>
        <p class="pequeno">${escapar(p.email)}</p>
      </div>
    </div>
    <div class="cuadricula">
      ${dato("Partidas", e.partidasJugadas)}
      ${dato("Victorias", e.victorias)}
      ${dato("Derrotas", e.derrotas)}
      ${dato("Puntos", e.puntosTotales)}
      ${dato("Mejor", e.mejorPuntaje)}
      ${dato("Promedio", Number(e.promedioPuntos).toFixed(1))}
    </div>
    <h3>Logros</h3>
    <ul class="lista">
      ${p.logros.map((l) => `
        <li class="logro${l.obtenido ? " obtenido" : ""}">
          <strong>${l.obtenido ? "🏆" : "🔒"} ${escapar(l.nombre)}</strong>
          <span class="pequeno">${escapar(l.descripcion)}</span>
          <progress max="${l.meta}" value="${Math.min(l.progreso, l.meta)}"></progress>
          <span class="pequeno">${l.progreso} / ${l.meta}</span>
        </li>`).join("")}
    </ul>`;
}

// ---------- Ranking ----------

function medalla(posicion) {
    return { 1: "🥇", 2: "🥈", 3: "🥉" }[posicion] ?? `${posicion}.`;
}

async function cargarRanking() {
    const lista = await api(`/api/ranking?criterio=${estado.criterio}&limite=10`);
    $("lista-ranking").innerHTML = lista.length === 0
        ? `<li class="vacio">Todavía no hay partidas jugadas.</li>`
        : lista.map((r) => `
        <li class="fila${r.usuarioId === estado.usuario?.id ? " yo" : ""}">
          <span class="posicion">${medalla(r.posicion)}</span>
          ${avatar(r.avatarId, r.username)}
          <span class="columna">
            <span class="nombre">${escapar(r.username)}</span>
            <span class="pequeno">${r.victorias} victorias · ${r.partidas} partidas · ${r.puntosTotales} pts</span>
          </span>
        </li>`).join("");
}

function cambiarCriterio(boton) {
    estado.criterio = boton.dataset.criterio;
    document.querySelectorAll("[data-criterio]").forEach((b) => b.classList.toggle("activa", b === boton));
    cargarRanking().catch((error) => mostrarError("app-error", error.message));
}

// ---------- Historial ----------

const textoResultado = { Victoria: "🏆 Victoria", Derrota: "Derrota", SinGanador: "Sin ganador" };

async function cargarHistorial() {
    const lista = await api("/api/perfil/historial?limite=20");
    $("lista-historial").innerHTML = lista.length === 0
        ? `<li class="vacio">Todavía no jugaste ninguna partida.</li>`
        : lista.map((h) => {
            const rivales = h.jugadores
                .filter((j) => j.usuarioId !== estado.usuario?.id)
                .map((j) => `${escapar(j.nombre)} (${j.puntaje})`)
                .join(", ");
            return `
          <li class="fila historial ${h.resultado.toLowerCase()}">
            <span class="columna">
              <span><strong>${textoResultado[h.resultado] ?? escapar(h.resultado)}</strong> · ${h.miPuntaje} pts</span>
              <span class="pequeno">${new Date(h.fin).toLocaleString("es-CR")} · duró ${mmss(h.duracionSegundos)}</span>
              <span class="pequeno">Contra: ${rivales || "—"}</span>
            </span>
          </li>`;
        }).join("");
}

// ---------- Partida en vivo (solo ver) ----------

async function cargarVivo() {
    cerrarSocket();
    const caja = $("vivo-contenido");
    caja.innerHTML = `<p class="vacio">Buscando tu partida…</p>`;

    const activa = await api("/api/partidas/activa");
    if ($("seccion-vivo").hidden) return;           // cambió de pestaña mientras esperaba

    if (!activa) {
        caja.innerHTML = `
      <div class="tarjeta centro">
        <p>No estás en ninguna partida.</p>
        <p class="pequeno">Entrá a una partida desde Unity y tocá "Buscar de nuevo".</p>
        <button type="button" id="btn-buscar-partida" class="secundario">Buscar de nuevo</button>
      </div>`;
        $("btn-buscar-partida").addEventListener("click", reintentarVivo);
        return;
    }
    mirarPartida(activa.id);
}

function reintentarVivo() {
    mostrarError("app-error", "");
    cargarVivo().catch((error) => mostrarError("app-error", error.message));
}

function mirarPartida(partidaId) {
    const protocolo = location.protocol === "https:" ? "wss" : "ws";
    const socket = new WebSocket(`${protocolo}://${location.host}/ws/partida?token=${encodeURIComponent(estado.token)}`);
    estado.socket = socket;

    socket.onopen = () => socket.send(JSON.stringify({ type: "match:watch", data: { matchId: partidaId } }));

    socket.onmessage = (e) => {
        let mensaje;
        try { mensaje = JSON.parse(e.data); } catch { return; }
        manejarMensaje(mensaje);
    };

    socket.onclose = () => {
        if (estado.socket !== socket) return;         // lo cerramos nosotros
        estado.socket = null;
        if (estado.partida?.status === "Finalizada") return;
        $("vivo-contenido").insertAdjacentHTML("beforeend", `
      <div class="tarjeta centro">
        <p class="error">Se perdió la conexión con el servidor.</p>
        <button type="button" id="btn-reconectar" class="secundario">Reconectar</button>
      </div>`);
        $("btn-reconectar").addEventListener("click", reintentarVivo);
    };
}

function cerrarSocket() {
    const socket = estado.socket;
    estado.socket = null;
    estado.partida = null;
    if (socket) socket.close();
}

function actualizarPuntos(usuarioId, puntos) {
    const jugador = estado.partida?.jugadores.find((j) => j.id === usuarioId);
    if (jugador) jugador.puntos = puntos;
}

function manejarMensaje({ type, data }) {
    if (type === "error") {
        mostrarError("app-error", data?.message);
        return;
    }

    if (type === "match:state") {
        estado.partida = {
            status: data.status,
            endValue: data.endValue,
            timeLeft: data.timeLeft,
            timeElapsed: data.timeElapsed,
            reactor: data.reactor,
            evento: estado.partida?.evento ?? null,
            final: null,
            jugadores: data.players.map((j) => ({ id: j.userId, nombre: j.username, avatarId: j.avatarId, puntos: j.score })),
        };
        dibujarPartida();
        return;
    }

    const p = estado.partida;
    if (!p) return;                                  // todavía no llegó la primera foto

    switch (type) {
        case "match:tick":
            if (p.status === "Espera") p.status = "EnCurso";   // si arrancó sin que llegara un match:state nuevo
            p.timeLeft = data.timeLeft;
            p.timeElapsed = data.timeElapsed;
            break;
        case "match:overtime":
            p.timeLeft = data.timeLeft;
            p.evento = `¡Empate! Prórroga de ${data.extraMinutes} minutos.`;
            break;
        case "score:changed":
        case "player:moved":
            actualizarPuntos(data.userId, data.score);
            break;
        case "player:extracted":
            actualizarPuntos(data.userId, data.score);
            if (p.reactor) {
                p.reactor.energy = data.reactorEnergy;
                p.reactor.state = data.reactorState;
            }
            break;
        case "event:started":
            p.evento = data.message;
            break;
        case "event:ended":
            p.evento = null;
            break;
        case "match:finished":
            p.status = "Finalizada";
            p.final = data;
            p.evento = null;
            for (const s of data.scores) actualizarPuntos(s.userId, s.score);
            break;
        default:
            return;                                      // arena:update, player:frozen...: el celular no los dibuja
    }
    dibujarPartida();
}

const textoEstado = { Espera: "Sala de espera", EnCurso: "En curso", Finalizada: "Terminada" };

function dibujarPartida() {
    const p = estado.partida;
    if (!p) return;

    const ordenados = [...p.jugadores].sort((a, b) => b.puntos - a.puntos);
    const tiempo = p.timeLeft == null
        ? `⏱ ${mmss(p.timeElapsed)} · meta ${p.endValue} pts`
        : `⏱ ${mmss(p.timeLeft)}`;

    const reactor = p.reactor ? `
    <div class="reactor">
      <span>⚛ Reactor</span>
      <progress max="${p.reactor.capacity}" value="${p.reactor.energy}"></progress>
      <span class="pequeno">${p.reactor.energy} / ${p.reactor.capacity}${p.reactor.state === "DISABLED" ? " · desactivado" : ""}</span>
    </div>` : "";

    let final = "";
    if (p.final) {
        const ganador = p.final.scores.find((s) => s.userId === p.final.winnerId);
        final = `<div class="tarjeta final">${ganador ? `🏆 Ganó <strong>${escapar(ganador.username)}</strong>` : "Terminó sin ganador"}</div>`;
    }

    $("vivo-contenido").innerHTML = `
    <div class="tarjeta">
      <div class="fila-estado">
        <span>${textoEstado[p.status] ?? escapar(p.status)}</span>
        <span class="tiempo">${tiempo}</span>
      </div>
      ${reactor}
    </div>
    ${p.evento ? `<div class="tarjeta evento">${escapar(p.evento)}</div>` : ""}
    ${final}
    <ul class="lista">
      ${ordenados.map((j, i) => `
        <li class="fila${j.id === estado.usuario?.id ? " yo" : ""}">
          <span class="posicion">${i + 1}.</span>
          ${avatar(j.avatarId, j.nombre)}
          <span class="nombre columna">${escapar(j.nombre)}</span>
          <span class="puntos">${j.puntos} pts</span>
        </li>`).join("")}
    </ul>
    <p class="pequeno centro">Te movés con los botones de tu control. Esta pantalla solo muestra el marcador.</p>`;
}

// ---------- Arranque ----------

$("form-login").addEventListener("submit", entrar);
$("btn-github").addEventListener("click", iniciarGitHub);
$("btn-github-cancelar").addEventListener("click", terminarGitHub);
$("btn-salir").addEventListener("click", () => cerrarSesion());
document.querySelectorAll("[data-seccion]").forEach((b) => b.addEventListener("click", () => cambiarSeccion(b.dataset.seccion)));
document.querySelectorAll("[data-criterio]").forEach((b) => b.addEventListener("click", () => cambiarCriterio(b)));

// Si quedó una sesión guardada, entra directo. Si el token venció, el servidor responde 401 y vuelve al login.
const guardada = leerSesion();
if (guardada?.token) {
    estado.token = guardada.token;
    estado.usuario = guardada.usuario;
    mostrarApp();
} else {
    mostrarLogin();
}