"use strict";
/* global Terminal, FitAddon */
/*
 * Browser Terminal — фронтенд терминала.
 * Общается с локальным native messaging хостом (host/host.py) через
 * chrome.runtime.connectNative. Только эта страница расширения может
 * отправлять команды — у расширения нет externally_connectable/content_scripts.
 */

const HOST_NAME = "com.korimuspast.browserterminal";
const VERSION = "1.0.0";

const term = new Terminal({
  cursorBlink: true,
  cursorStyle: "bar",
  fontSize: 14,
  fontFamily: "ui-monospace, 'Cascadia Code', Menlo, Consolas, 'DejaVu Sans Mono', monospace",
  scrollback: 10000,
  convertEol: false,
  theme: {
    background: "#0d1117",
    foreground: "#c9d1d9",
    cursor: "#58d68d",
    selectionBackground: "#264f78",
    black: "#484f58", red: "#ff7b72", green: "#3fb950", yellow: "#d29922",
    blue: "#58a6ff", magenta: "#bc8cff", cyan: "#39c5cf", white: "#b1bac4",
    brightBlack: "#6e7681", brightRed: "#ffa198", brightGreen: "#56d364",
    brightYellow: "#e3b341", brightBlue: "#79c0ff", brightMagenta: "#d2a8ff",
    brightCyan: "#56d4dd", brightWhite: "#f0f6fc"
  }
});
const fitAddon = new FitAddon();
term.loadAddon(fitAddon);
const termEl = document.getElementById("terminal");
term.open(termEl);
try { fitAddon.fit(); } catch (e) { /* маловероятно */ }
window.addEventListener("resize", () => { try { fitAddon.fit(); } catch (e) {} });

const state = {
  port: null,
  running: false,
  currentId: 0,
  line: "",
  cursor: 0,
  history: [],
  hIndex: 0,
  user: "user",
  host: "host",
  cwd: "~",
  home: ""
};

const statusEl = document.getElementById("status");
function setStatus(text, ok) {
  statusEl.textContent = text;
  statusEl.classList.toggle("ok", !!ok);
  statusEl.classList.toggle("bad", !ok);
}

function extIdHint() {
  try { return chrome.runtime.id; } catch (e) { return "<ID_РАСШИРЕНИЯ>"; }
}

function loadHistory() {
  try {
    chrome.storage.local.get(["bt-history"], (res) => {
      if (Array.isArray(res["bt-history"])) state.history = res["bt-history"];
      state.hIndex = state.history.length;
    });
  } catch (e) { /* storage недоступен — просто без истории */ }
}
function saveHistory() {
  try { chrome.storage.local.set({ "bt-history": state.history.slice(-500) }); } catch (e) {}
}

function promptStr() {
  let cwd = state.cwd || "~";
  if (state.home && (cwd === state.home || cwd.startsWith(state.home + "/"))) {
    cwd = "~" + cwd.slice(state.home.length);
  }
  return state.user + "@" + state.host + ":" + cwd + "$ ";
}

function printPrompt() {
  term.write(promptStr());
}

function redrawLine() {
  term.write("\r" + promptStr() + state.line + "\x1b[K");
  const back = state.line.length - state.cursor;
  if (back > 0) term.write("\x1b[" + back + "D");
}

function submitLine() {
  const cmd = state.line;
  term.write("\r\n");
  state.line = "";
  state.cursor = 0;
  if (cmd.trim().length === 0) {
    printPrompt();
    return;
  }
  state.history.push(cmd);
  if (state.history.length > 500) state.history.shift();
  state.hIndex = state.history.length;
  saveHistory();
  runCommand(cmd);
}

function runCommand(cmd) {
  if (!state.port) {
    term.write("\x1b[31mНет соединения с локальным хостом.\x1b[0m\r\n");
    term.write("Установите хост: ./install.sh " + extIdHint() + "  (см. README.md), затем нажмите «⟳ Переподключиться».\r\n");
    printPrompt();
    return;
  }
  state.running = true;
  state.currentId += 1;
  state.port.postMessage({ kind: "run", id: state.currentId, command: cmd });
}

function sendToHost(msg) {
  if (state.port) {
    try { state.port.postMessage(msg); } catch (e) { /* порт уже закрыт */ }
  }
}

function onHostMessage(msg) {
  if (!msg || typeof msg !== "object") return;
  switch (msg.kind) {
    case "ready":
      state.user = msg.user || state.user;
      state.host = msg.host || state.host;
      state.cwd = msg.cwd || state.cwd;
      state.home = msg.home || "";
      setStatus("● подключено: " + (msg.shell || "shell") + " • " + (msg.user || "") + "@" + (msg.host || ""), true);
      printPrompt();
      break;
    case "out":
      if (msg.id === state.currentId) term.write(String(msg.data));
      break;
    case "err":
      if (msg.id === state.currentId) term.write(String(msg.data));
      break;
    case "done":
      if (msg.cwd) state.cwd = msg.cwd;
      if (msg.id === state.currentId) {
        state.running = false;
        printPrompt();
      }
      break;
    case "error":
      term.write("\r\n\x1b[31m[хост] " + String(msg.message).replace(/\r?\n/g, "\r\n") + "\x1b[0m\r\n");
      if (msg.id === state.currentId) {
        state.running = false;
        printPrompt();
      }
      break;
    case "pong":
      break;
  }
}

let bannerShown = false;
function showBanner() {
  if (bannerShown) return;
  bannerShown = true;
  term.write("\x1b[1;32mBrowser Terminal v" + VERSION + "\x1b[0m — локальный терминал: команды выполняются в вашей системе.\r\n");
  term.write("Примеры: \x1b[36mls -la\x1b[0m, \x1b[36mcd /tmp\x1b[0m, \x1b[36mgit status\x1b[0m, \x1b[36mpython3 --version\x1b[0m, \x1b[36mhtop\x1b[0m (интерактивные — без ввода)\r\n");
  term.write("Ctrl+C — прервать команду, Ctrl+L — очистить экран, ↑/↓ — история, Ctrl+U — стереть строку.\r\n\r\n");
}

function connect() {
  if (state.port) {
    try { state.port.disconnect(); } catch (e) {}
    state.port = null;
  }
  setStatus("подключение…", false);
  let port;
  try {
    port = chrome.runtime.connectNative(HOST_NAME);
  } catch (e) {
    setStatus("ошибка подключения", false);
    term.write("\r\n\x1b[31mНе удалось подключиться к native host «" + HOST_NAME + "»: " + e.message + "\x1b[0m\r\n");
    term.write("Установите хост: ./install.sh " + extIdHint() + " (см. README.md)\r\n");
    return;
  }
  state.port = port;
  port.onMessage.addListener(onHostMessage);
  port.onDisconnect.addListener(() => {
    const err = chrome.runtime.lastError;
    state.port = null;
    state.running = false;
    setStatus("нет соединения", false);
    term.write("\r\n\x1b[31m[Соединение с локальным хостом закрыто]\x1b[0m\r\n");
    if (err && err.message) term.write("  " + err.message + "\r\n");
    term.write("  Если хост не установлен: ./install.sh " + extIdHint() + "  (см. README.md)\r\n");
    printPrompt();
  });
}

term.onData((data) => {
  // --- навигация по истории и курсор ---
  if (data === "\x1b[A") { // стрелка вверх
    if (state.hIndex > 0) {
      state.hIndex -= 1;
      state.line = state.history[state.hIndex] || "";
      state.cursor = state.line.length;
      redrawLine();
    }
    return;
  }
  if (data === "\x1b[B") { // стрелка вниз
    if (state.hIndex < state.history.length) {
      state.hIndex += 1;
      state.line = state.history[state.hIndex] || "";
      state.cursor = state.line.length;
      redrawLine();
    }
    return;
  }
  if (data === "\x1b[C") { // вправо
    if (state.cursor < state.line.length) { state.cursor += 1; term.write("\x1b[C"); }
    return;
  }
  if (data === "\x1b[D") { // влево
    if (state.cursor > 0) { state.cursor -= 1; term.write("\x1b[D"); }
    return;
  }
  if (data === "\x1b[H" || data === "\x1b[1~") { state.cursor = 0; redrawLine(); return; }
  if (data === "\x1b[F" || data === "\x1b[4~") { state.cursor = state.line.length; redrawLine(); return; }
  if (data === "\x1b[3~") { // Delete
    if (!state.running && state.cursor < state.line.length) {
      state.line = state.line.slice(0, state.cursor) + state.line.slice(state.cursor + 1);
      redrawLine();
    }
    return;
  }

  // --- во время выполнения команды принимаем только Ctrl+C ---
  if (state.running) {
    if (data === "\x03") {
      sendToHost({ kind: "interrupt" });
      state.currentId = -1; // поздний «done» от убитой команды игнорируем
      state.running = false;
      term.write("^C\r\n");
      printPrompt();
    }
    return;
  }

  // --- редактирование строки ---
  if (data === "\r") { submitLine(); return; }
  if (data === "\x7f") { // Backspace
    if (state.cursor > 0) {
      state.line = state.line.slice(0, state.cursor - 1) + state.line.slice(state.cursor);
      state.cursor -= 1;
      redrawLine();
    }
    return;
  }
  if (data === "\x03") { // Ctrl+C на пустой/готовой строке
    term.write("^C\r\n");
    state.line = "";
    state.cursor = 0;
    printPrompt();
    return;
  }
  if (data === "\x0c") { // Ctrl+L — очистить экран
    term.reset();
    printPrompt();
    return;
  }
  if (data === "\x15") { // Ctrl+U — стереть всю строку
    state.line = "";
    state.cursor = 0;
    redrawLine();
    return;
  }
  if (data === "\x17") { // Ctrl+W — стереть слово
    let s = state.line.slice(0, state.cursor);
    s = s.replace(/\S+\s*$/, "").replace(/\s+$/, "");
    state.line = s + state.line.slice(state.cursor);
    state.cursor = s.length;
    redrawLine();
    return;
  }
  if (data === "\t") return; // Tab не вставляем
  if (data < " ") return;    // прочие управляющие символы игнорируем
  // печатаемый текст (в т.ч. многосимвольная вставка из буфера обмена)
  state.line = state.line.slice(0, state.cursor) + data + state.line.slice(state.cursor);
  state.cursor += data.length;
  redrawLine();
});

document.getElementById("btn-clear").addEventListener("click", () => {
  term.reset();
  if (!state.running) printPrompt();
  term.focus();
});
document.getElementById("btn-reconnect").addEventListener("click", () => {
  term.write("\r\n[переподключение к хосту…]\r\n");
  connect();
  term.focus();
});
termEl.addEventListener("click", () => term.focus());

loadHistory();
showBanner();
connect();
term.focus();
