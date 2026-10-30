import RFB from './vendor/novnc/core/rfb.js';

const $ = (id) => document.getElementById(id);
const screen = $('screen');
const overlay = $('overlay');
const overlayTitle = $('overlay-title');
const overlayText = $('overlay-text');
const overlayConnect = $('btn-overlay-connect');
const badgeQemu = $('badge-qemu');
const badgeRfb = $('badge-rfb');
const badgeReady = $('badge-ready');
const stageSub = $('stage-sub');
const btnConnect = $('btn-connect');
const toast = $('toast');

let rfb = null;
let connected = false;
let ready = false;
let reconnectTimer = null;
let reconnectDelay = 1000;
let manualDisconnect = false;
let toastTimer = null;

function setBadge(el, text, cls) {
  el.textContent = text;
  el.className = 'badge' + (cls ? ' ' + cls : '');
}
function showToast(msg) {
  toast.textContent = msg;
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toast.hidden = true; }, 3500);
}
function showOverlay(title, text, canConnect = false) {
  overlayTitle.textContent = title;
  overlayText.textContent = text;
  overlayConnect.hidden = !canConnect;
  overlay.hidden = false;
}
function hideOverlay() { overlay.hidden = true; }

function wsUrl() {
  const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${proto}//${location.host}/websockify`;
}

function updateConnectButton() {
  btnConnect.textContent = connected ? 'Отключиться' : 'Подключиться';
}

function connect() {
  if (rfb) return;
  manualDisconnect = false;
  clearTimeout(reconnectTimer);
  showOverlay('Подключаемся к экрану ВМ…', 'Это настоящий вывод QEMU, а не картинка-макет.');
  setBadge(badgeRfb, 'Экран: подключение…', 'warn');
  try {
    rfb = new RFB(screen, wsUrl(), { shared: true, wsProtocols: ['binary'] });
  } catch (e) {
    rfb = null;
    setBadge(badgeRfb, 'Экран: ошибка', 'err');
    showOverlay('Не удалось создать VNC-клиент', String(e.message || e), true);
    return;
  }
  rfb.scaleViewport = true;
  rfb.resizeSession = false;
  rfb.viewOnly = false;
  rfb.focusOnClick = true;
  rfb.addEventListener('connect', () => {
    connected = true;
    reconnectDelay = 1000;
    setBadge(badgeRfb, 'Экран: подключён', 'ok');
    updateConnectButton();
    if (ready) hideOverlay();
    else showOverlay('Aurora загружается…', 'Экран подключён. Ждём готовности рабочего стола.');
    screen.focus();
  });
  rfb.addEventListener('disconnect', (ev) => {
    connected = false;
    rfb = null;
    updateConnectButton();
    setBadge(badgeRfb, 'Экран: отключён', 'warn');
    if (manualDisconnect) {
      showOverlay('Экран отключён', 'Нажмите «Подключиться», чтобы снова управлять Aurora.', true);
      return;
    }
    showOverlay('Соединение с экраном потеряно', 'Пробуем переподключиться автоматически…', true);
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connect, reconnectDelay);
    reconnectDelay = Math.min(reconnectDelay * 2, 10000);
  });
  rfb.addEventListener('securityfailure', (ev) => {
    setBadge(badgeRfb, 'Экран: ошибка безопасности', 'err');
    showOverlay('VNC security failure', String(ev.detail?.reason || 'unknown'), true);
  });
  rfb.addEventListener('credentialsrequired', () => {
    showOverlay('VNC требует пароль', 'Этот preview настроен без пароля; сообщите об ошибке.', true);
  });
}

function disconnect() {
  manualDisconnect = true;
  clearTimeout(reconnectTimer);
  if (rfb) { rfb.disconnect(); rfb = null; }
  connected = false;
  updateConnectButton();
  setBadge(badgeRfb, 'Экран: отключён', 'warn');
  showOverlay('Экран отключён', 'Нажмите «Подключиться», чтобы снова управлять Aurora.', true);
}

async function api(path, opts = {}) {
  const res = await fetch(path, {
    ...opts,
    headers: { 'Content-Type': 'application/json', 'X-Aurora-Control': '1', ...(opts.headers || {}) },
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

async function pollStatus() {
  try {
    const s = await api('/api/status', { headers: {} });
    const running = s.qmp?.running || s.qemu === 'running';
    setBadge(badgeQemu, running ? 'QEMU: работает' : `QEMU: ${s.qemu}`, running ? 'ok' : 'warn');
    ready = !!s.ready;
    setBadge(badgeReady, ready ? 'ОС: рабочий стол готов' : 'ОС: загрузка…', ready ? 'ok' : 'warn');
    stageSub.textContent = `${s.firmware || 'BIOS'} · ${s.resolution || '1024x768'} · 512 МБ RAM`;
    if (connected && ready) hideOverlay();
  } catch (e) {
    setBadge(badgeQemu, 'QEMU: недоступен', 'err');
  }
}

async function refreshSerial() {
  try {
    const res = await fetch('/api/serial');
    $('serial-log').textContent = await res.text();
    $('serial-log').scrollTop = $('serial-log').scrollHeight;
  } catch (e) {
    $('serial-log').textContent = String(e.message || e);
  }
}

btnConnect.addEventListener('click', () => connected ? disconnect() : connect());
overlayConnect.addEventListener('click', connect);
$('btn-fullscreen').addEventListener('click', async () => {
  try {
    if (document.fullscreenElement) await document.exitFullscreen();
    else await $('screen-wrap').requestFullscreen();
  } catch (e) { showToast('Полный экран недоступен: ' + (e.message || e)); }
});
$('btn-cad').addEventListener('click', async () => {
  try {
    if (connected && rfb) rfb.sendCtrlAltDel();
    else await api('/api/send-key', { method: 'POST', body: JSON.stringify({ keys: ['ctrl', 'alt', 'delete'] }) });
    showToast('Ctrl+Alt+Del отправлен в ВМ');
  } catch (e) { showToast('Не удалось отправить клавиши: ' + (e.message || e)); }
});
$('btn-reboot').addEventListener('click', async () => {
  if (!confirm('Перезагрузить виртуальную машину Aurora? Текущее состояние ОС будет потеряно.')) return;
  try {
    await api('/api/reboot', { method: 'POST' });
    ready = false;
    setBadge(badgeReady, 'ОС: перезагрузка…', 'warn');
    if (connected) showOverlay('Перезагрузка Aurora…', 'QEMU выполняет аппаратный сброс.');
    showToast('Команда перезагрузки отправлена');
  } catch (e) { showToast('Ошибка перезагрузки: ' + (e.message || e)); }
});
$('btn-serial').addEventListener('click', () => {
  const panel = $('serial-panel');
  panel.hidden = !panel.hidden;
  $('btn-serial').setAttribute('aria-expanded', String(!panel.hidden));
  if (!panel.hidden) refreshSerial();
});
$('btn-serial-refresh').addEventListener('click', refreshSerial);
$('btn-help').addEventListener('click', () => {
  const panel = $('help');
  panel.hidden = !panel.hidden;
  $('btn-help').setAttribute('aria-expanded', String(!panel.hidden));
});

setInterval(pollStatus, 2000);
setInterval(() => { if (!$('serial-panel').hidden) refreshSerial(); }, 2500);
pollStatus();
connect();
