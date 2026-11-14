'use strict';
/* ===== Запуск и главный цикл ===== */
let acc = 0, lastT = 0, lastUi = 0, autoSave = 0, crashN = 0;
function startGame(S) {
  UI.S = S; RN.S = S; RN.sel = null; RN.place = null; RN.mask = null;
  UI.sheet = null; UI.shownOver = false; UI.paused = false; UI.speed = 1; UI.toasts = []; UI.tab = 'map'; UI.bpage = 0; UI.inboxTab = 'all'; UI.inboxNote = '';
  UI.hud = {}; UI.seenMsg = undefined; UI.planWas = undefined; UI.fxT = S.time; UI.achT = 0; UI.mmN = 0;
  closeModal(); for (const id of ['menu', 'create', 'sheet', 'mm', 'loading']) $(id).classList.add('hidden');
  $('hud').classList.remove('hidden'); $('fade').classList.remove('on');
  DEMO.S = null; buildTerrain(S); RN.minimap = $('mm'); RN.cam.z = 3;
  const c = capitalOf(S, 0); if (c) centerOn(c.x + 1, c.y + 1); else centerOn(S.W / 2, S.H / 2);
  // счётчики времени начинаем заново: иначе после загрузки игра «догоняла» время прошлой партии, а автосохранение молчало
  acc = 0; lastT = performance.now(); lastUi = 0; autoSave = S.time; crashN = 0;
  calcRates(S); calcScore(S); calcVision(S); renderTabs();
  if (S.time < 1 && !S.msgs.length) info(S, 0, 'Добро пожаловать!', SET.hints ? 'Следуйте плану развития — он на свитке вверху экрана.' : 'Ваше королевство ждёт приказов.', 'info');
  updateHud();
}

/* ----- фон меню: живой мир, в котором воюют соперники ----- */
const DEMO = { S: null, warm: 0, acc: 0, t: 0, next: 0, last: -1e9, ready: false, drift: null };
function initDemo() {
  DEMO.S = null; DEMO.last = performance.now();
  try {
    const S = newGame({ type: MAPTYPES[(Math.random() * MAPTYPES.length) | 0], size: 's', bots: 3, diff: 'hard', win: 'caps', nation: (Math.random() * PNAMES.length) | 0, events: 0 });
    for (const p of S.players) p.human = false;
    // первые минуты мир «проживает» ускоренно, понемногу в каждом кадре: раньше запуск игры замирал, пока шёл этот расчёт
    DEMO.S = S; DEMO.warm = 1200; DEMO.acc = 0; DEMO.t = 0; DEMO.ready = false; DEMO.next = 0; DEMO.drift = null;
    buildTerrain(S); RN.S = S; RN.sel = null; RN.place = null;
  } catch (e) { DEMO.S = null; }
}
function demoScene(S) {
  const pts = [];
  for (const c of S.cities) if (c.owner >= 0) pts.push([c.x + 1, c.y + 1, 4.4]);
  for (const a of S.armies) if (!a.ship) pts.push([a.x, a.y, a.fight ? 6 : 4.6], [a.x, a.y, a.fight ? 6 : 4.6]);
  for (const s of S.ships) pts.push([s.x, s.y, 5]);
  for (const b of S.buildings) if (b.owner >= 0 && (b.type === 'port' || b.type === 'barracks' || b.type === 'mine' || b.type === 'mill')) pts.push([b.x, b.y, 5]);
  if (!pts.length) pts.push([S.W / 2, S.H / 2, 3]);
  const p = pts[(Math.random() * pts.length) | 0]; RN.cam.z = p[2]; centerOn(p[0], p[1]); DEMO.drift = { x: (Math.random() - 0.5) * 6, y: (Math.random() - 0.5) * 4 };
}
function menuFrame(now) {
  if (!$('create').classList.contains('hidden')) return; // экран настроек партии непрозрачный — фон под ним не считаем
  const S = DEMO.S; if (!S) { frame(null, now); if (now - DEMO.last > 1500) initDemo(); return; }
  const dt = Math.min(0.1, Math.max(0, (now - (DEMO.t || now)) / 1000)); DEMO.t = now;
  if (DEMO.warm > 0) { const t0 = performance.now(); while (DEMO.warm > 0 && !S.over && performance.now() - t0 < 6) { tick(S, 0.25); DEMO.warm--; } }
  else { DEMO.acc += dt; if (DEMO.acc >= 0.25) { tick(S, 0.25); DEMO.acc = Math.min(0.25, DEMO.acc - 0.25); } }
  if (S.over) { DEMO.S = null; DEMO.last = now; return; }
  if (now > DEMO.next) {
    const f = $('fade');
    if (!DEMO.ready) { demoScene(S); DEMO.ready = true; DEMO.next = now + 7000; f.classList.remove('on'); }
    else { f.classList.add('on'); DEMO.next = now + 700; DEMO.ready = false; }
  }
  if (DEMO.drift) { RN.cam.x += DEMO.drift.x * dt; RN.cam.y += DEMO.drift.y * dt; clampCam(); }
  frame(S, now);
}

/* ----- звуки боя: только то, что игрок видит на экране ----- */
function battleSounds(S) {
  const fx = S.fx, t0 = UI.fxT, cam = RN.cam; let newest = t0, hit = false, arr = false, rock = false;
  for (let i = fx.length - 1; i >= 0; i--) {
    const f = fx[i]; if (f.t <= t0) break; if (f.t > newest) newest = f.t;
    const k = f.k === 'hit' ? 1 : f.k === 'arr' ? 2 : (f.k === 'rock' || f.k === 'rubble') ? 3 : 0;
    if (!k || (k === 1 && hit) || (k === 2 && arr) || (k === 3 && rock)) continue;
    const x = f.x !== undefined ? f.x : f.x0, y = f.y !== undefined ? f.y : f.y0, sx = (x * TS - cam.x) * cam.z, sy = (y * TS - cam.y) * cam.z;
    if (sx < 0 || sy < 0 || sx > RN.w || sy > RN.h || !visibleAt(S, x, y)) continue;
    if (k === 1) hit = true; else if (k === 2) arr = true; else rock = true;
  }
  UI.fxT = newest;
  if (hit) SND.play('clash'); if (arr) SND.play('arrow'); if (rock) SND.play('rock');
}

function step(now) {
  const S = UI.S; if (!S) { menuFrame(now); return; }
  const dt = Math.min(0.1, Math.max(0, (now - lastT) / 1000)); lastT = now;
  // пока открыто окно, игра стоит; сама «пауза» игрока при этом не трогается
  if (!UI.paused && !S.over && !UI.modal) {
    acc += dt * UI.speed; let n = 0;
    while (acc >= 0.25 && n < 4 && !S.over) { tick(S, 0.25); acc -= 0.25; n++; }
    if (acc >= 0.25) acc = 0;
    if (n) battleSounds(S);
  }
  frame(S, now);
  if (now - lastUi > 250) { lastUi = now; updateHud(); }
  if (!S.over && S.time - autoSave > 60) { autoSave = S.time; saveGame(S); }
  if (S.over && !UI.shownOver) { UI.shownOver = true; showGameOver(); }
}
function loop(now) {
  requestAnimationFrame(loop);
  try { step(now); crashN = 0; }
  catch (e) {
    // один сбой в кадре не должен ронять игру; если сбой повторяется — останавливаем партию и говорим об этом
    crashN++; if (crashN === 1) { try { console.error(e); } catch (x) { /* консоли может не быть */ } }
    if (crashN === 5) {
      if (UI.S) { UI.paused = true; try { renderTabs(); showModal('Сбой', '<div>В игре произошёл сбой, партия поставлена на паузу.</div><div class="dim">Можно попробовать продолжить. Если сбой повторится, выйдите в меню: партия сохранится.</div>', btn('closemodal', 'Продолжить') + btn('tomenu', 'В главное меню', 'gold')); } catch (x) { /* окно показать не удалось */ } }
      else { DEMO.S = null; DEMO.last = now; }
    }
  }
}
function saveNow() { const S = UI.S; if (S && !S.over) saveGame(S); }

window.addEventListener('load', () => {
  initIcons(); RN.cv = $('cv'); RN.ctx = RN.cv.getContext('2d'); resize(); initInput();
  window.addEventListener('resize', () => { resize(); clampCam(); });
  // значки в неизменяемой части страницы
  $('scoreI').innerHTML = IMG('medal', 22); $('inboxIc').innerHTML = IMG('mail', 28); $('burger').innerHTML = IMG('menu', 22);
  $('mAch').innerHTML = IMG('star', 22); $('mRec').innerHTML = IMG('trophy', 22); $('mSet').innerHTML = IMG('gear', 22);
  $('ver').textContent = 'версия ' + GAME_VERSION;
  applySettings(); refreshMenu();
  if (document.fonts && document.fonts.load) { try { document.fonts.load('16px PixelRU'); } catch (e) { /* шрифт подгрузится сам */ } }
  requestAnimationFrame(loop);
  window.__kcReady = true; // запуск прошёл: экран сбоя из boot.js больше не нужен
});
// приложение свернули или закрыли — партия сохраняется
document.addEventListener('visibilitychange', () => { if (document.hidden) saveNow(); });
window.addEventListener('pagehide', saveNow);
window.__kcPause = saveNow; // вызывает оболочка приложения, когда оно уходит в фон
window.__kcResume = () => { lastT = performance.now(); DEMO.t = 0; }; // вернулись в игру: время простоя не засчитывается
document.addEventListener('contextmenu', e => e.preventDefault());
