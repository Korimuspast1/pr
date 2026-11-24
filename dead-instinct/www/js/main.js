'use strict';
/* =====================================================================
   DEAD INSTINCT — запуск, ввод, главный цикл
   ===================================================================== */

const MOVE = { mx: 0, my: 0, sneak: false, sprintHold: false, stick: null, camStick: null };
const GAME_INPUT = { mx: 0, my: 0, sneak: false, sprint: false, danger: false, freeze: false, flare: false, attack: false, ax: 0, ay: 0 };

let LAST = 0, ACC = 0, UIACC = 0, FPS = 0, FPSACC = 0, FRAMES = 0;

/* ---------- виртуальные стики ---------- */
function setupStick(el, onMove, onEnd) {
  let active = false, id = null, cx = 0, cy = 0;
  const knob = el.querySelector('.knob');
  const R = 46;
  const start = (e) => {
    const t = e.changedTouches ? e.changedTouches[0] : e;
    active = true; id = t.identifier;
    const r = el.getBoundingClientRect();
    cx = r.left + r.width / 2; cy = r.top + r.height / 2;
    move(e); e.preventDefault();
  };
  const move = (e) => {
    if (!active) return;
    let t = null;
    if (e.changedTouches) { for (const ch of e.changedTouches) if (ch.identifier === id) t = ch; }
    else t = e;
    if (!t) return;
    let dx = t.clientX - cx, dy = t.clientY - cy;
    const d = Math.hypot(dx, dy);
    if (d > R) { dx = dx / d * R; dy = dy / d * R; }
    knob.style.transform = `translate(${dx}px, ${dy}px)`;
    const nx = dx / R, ny = dy / R;
    const mag = Math.min(1, Math.hypot(nx, ny));
    const ang = Math.atan2(ny, nx);
    onMove(Math.cos(ang) * mag, Math.sin(ang) * mag);
    e.preventDefault();
  };
  const end = (e) => {
    if (!active) return;
    active = false; id = null;
    knob.style.transform = 'translate(0,0)';
    onEnd && onEnd();
  };
  el.addEventListener('touchstart', start, { passive: false });
  el.addEventListener('touchmove', move, { passive: false });
  el.addEventListener('touchend', end);
  el.addEventListener('touchcancel', end);
  el.addEventListener('mousedown', start);
  window.addEventListener('mousemove', (e) => { if (active && !e.touches) move(e); });
  window.addEventListener('mouseup', (e) => { if (active && !e.touches) end(e); });
}

function initInput() {
  // стик движения
  setupStick($('stickL'), (x, y) => { MOVE.mx = x; MOVE.my = y; }, () => { MOVE.mx = 0; MOVE.my = 0; });
  // стик камеры
  const camEl = $('stickR');
  setupStick(camEl, (x, y) => {
    if (!UI.G) return;
    CAM.follow = false;
    CAM.x += x * 6 * 0.016;
    CAM.y += y * 6 * 0.016;
    CAM.manualT = 1.2;
  }, () => { });
  // --- жесты на игровом поле: перетаскивание камеры, щипок, тап = удар ---
  const cv = $('cv');
  const GEST = { mode: null, id: null, sx: 0, sy: 0, moved: false, t: 0, pinch: 0, camX: 0, camY: 0 };
  const dist2 = (a, b) => Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
  const camDrag = (dx, dy) => {
    if (!UI.G) return;
    CAM.follow = false;
    CAM.manualT = 2.2;
    CAM.x -= dx / CAM.z;
    CAM.y -= dy / CAM.z;
    clampCam(UI.G);
  };
  cv.addEventListener('touchstart', (e) => {
    if (e.touches.length >= 2) {
      GEST.mode = 'pinch'; GEST.pinch = dist2(e.touches[0], e.touches[1]);
      GEST.t = performance.now(); GEST.moved = true;
      return;
    }
    const t = e.changedTouches[0];
    // левая нижняя четверть — стик (его ловит сам стик), остальное — камера/удар
    GEST.mode = 'drag'; GEST.id = t.identifier;
    GEST.sx = t.clientX; GEST.sy = t.clientY; GEST.camX = t.clientX; GEST.camY = t.clientY;
    GEST.moved = false; GEST.t = performance.now();
  }, { passive: true });
  cv.addEventListener('touchmove', (e) => {
    if (GEST.mode === 'pinch' && e.touches.length >= 2) {
      const d = dist2(e.touches[0], e.touches[1]);
      const k = d / (GEST.pinch || d);
      CAM.z = clamp(CAM.z * k, 18, 64);
      GEST.pinch = d;
      return;
    }
    let t = null;
    for (const ch of e.changedTouches) if (ch.identifier === GEST.id) t = ch;
    if (!t) return;
    const dx = t.clientX - GEST.camX, dy = t.clientY - GEST.camY;
    if (Math.hypot(t.clientX - GEST.sx, t.clientY - GEST.sy) > 12) GEST.moved = true;
    if (GEST.moved) { camDrag(dx, dy); GEST.camX = t.clientX; GEST.camY = t.clientY; e.preventDefault(); }
  }, { passive: false });
  const endTouch = (e) => {
    if (GEST.mode === 'pinch' && e.touches.length >= 2) return;
    if (GEST.mode !== 'drag') return;
    const t = e.changedTouches[0];
    if (!GEST.moved && t && performance.now() - GEST.t < 320) {
      const w = screenToWorld(t.clientX, t.clientY);
      if (UI.G) {
        const G = UI.G;
        // тап по объекту — обыскать/открыть, иначе — удар
        if (Math.hypot(w.x - G.player.x, w.y - G.player.y) > 2.2 && UI.interTarget) doInteract();
        else { GAME_INPUT.attack = true; GAME_INPUT.ax = w.x; GAME_INPUT.ay = w.y; }
      }
    }
    GEST.mode = null; GEST.id = null;
  };
  cv.addEventListener('touchend', endTouch, { passive: true });
  cv.addEventListener('touchcancel', endTouch, { passive: true });
  // мышь: ЛКМ — удар, ПКМ/средняя — тащить камеру, колесо — зум
  let mDown = null;
  cv.addEventListener('mousedown', (e) => {
    if (!UI.G) return;
    if (e.button === 0) {
      const w = screenToWorld(e.clientX, e.clientY);
      GAME_INPUT.attack = true; GAME_INPUT.ax = w.x; GAME_INPUT.ay = w.y;
    } else { mDown = { x: e.clientX, y: e.clientY }; }
  });
  cv.addEventListener('mousemove', (e) => {
    if (mDown) { camDrag(e.clientX - mDown.x, e.clientY - mDown.y); mDown = { x: e.clientX, y: e.clientY }; }
  });
  window.addEventListener('mouseup', () => { mDown = null; });
  window.addEventListener('wheel', (e) => {
    if (!UI.G) return;
    CAM.z = clamp(CAM.z - Math.sign(e.deltaY) * 2.5, 18, 64);
  }, { passive: true });

  // клавиатура
  const keys = new Set();
  window.addEventListener('keydown', (e) => {
    const k = e.key.toLowerCase();
    keys.add(k);
    if (k === 'escape') { if (UI.G) { if (UI.screen) closeScreen(); else openPause(); } return; }
    if (!UI.G) return;
    if (k === 'i' || k === 'tab') { toggleInv(); e.preventDefault(); }
    if (k === 'f') { const P = UI.G.player; if (P.battery > 0) P.torch = !P.torch; }
    if (k === 'c') { MOVE.sneak = !MOVE.sneak; }
    if (k === 'e') { doInteract(); }
    if (k === 'q') GAME_INPUT.danger = true;
    if (k === 'r') GAME_INPUT.freeze = true;
    if (k === 'x') { const P = UI.G.player; throwBang(UI.G, P.x + Math.cos(P.dir) * 5, P.y + Math.sin(P.dir) * 5); }
    if (k === ' ') { const P = UI.G.player; GAME_INPUT.attack = true; GAME_INPUT.ax = P.x + Math.cos(P.dir) * 2; GAME_INPUT.ay = P.y + Math.sin(P.dir) * 2; e.preventDefault(); }
    updateKeys(keys);
  });
  window.addEventListener('keyup', (e) => { keys.delete(e.key.toLowerCase()); updateKeys(keys); });
  cv.addEventListener('mousedown', (e) => {
    if (!UI.G) return;
    const w = screenToWorld(e.clientX, e.clientY);
    GAME_INPUT.attack = true; GAME_INPUT.ax = w.x; GAME_INPUT.ay = w.y;
  });
  // будильник первого касания для звука
  const unlock = () => { sndInit(); if (SND.ctx && SND.ctx.state === 'suspended') SND.ctx.resume(); };
  window.addEventListener('touchstart', unlock, { once: true });
  window.addEventListener('mousedown', unlock, { once: true });
}

function updateKeys(keys) {
  let mx = 0, my = 0;
  if (keys.has('w') || keys.has('arrowup') || keys.has('ц')) my -= 1;
  if (keys.has('s') || keys.has('arrowdown') || keys.has('ы')) my += 1;
  if (keys.has('a') || keys.has('arrowleft') || keys.has('ф')) mx -= 1;
  if (keys.has('d') || keys.has('arrowright') || keys.has('в')) mx += 1;
  GAME_INPUT.keyMx = mx; GAME_INPUT.keyMy = my;
  GAME_INPUT.sprint = keys.has('shift');
}

/* ---------- главный цикл ---------- */
function loop(now) {
  requestAnimationFrame(loop);
  if (!LAST) LAST = now;
  let dtRaw = (now - LAST) / 1000;
  LAST = now;
  if (dtRaw > 0.12) dtRaw = 0.12;
  FPSACC += dtRaw; FRAMES++;
  if (FPSACC > 1) { FPS = FRAMES / FPSACC; FPSACC = 0; FRAMES = 0; }

  const G = UI.G;
  if (!G) {
    renderMenuBg(dtRaw);
    return;
  }
  // пауза/модалки
  const frozen = G.paused || UI.modal || UI.screen === 'pause' || UI.screen === 'howto' || UI.screen === 'settings' || G.dialog;
  const timeScale = G.dangerSlow > 0 ? 0.32 : 1;
  const dt = frozen ? 0 : dtRaw * timeScale;

  if (!frozen) {
    ACC += dt;
    let steps = 0;
    const step = 1 / 60;
    const inp = gatherInput();
    while (ACC >= step && steps < 6) {
      ACC -= step; steps++;
      updatePlayerSim(G, step);
      tickWorld(G, step, inp);
    }
  } else {
    // всё равно нужен рендер и звук
    ACC = 0;
  }
  // рендер
  RN.dtScale = frozen ? 0 : timeScale;
  render(G, frozen ? 0 : dtRaw);
  ambientTick(G, frozen ? 0 : dtRaw);
  renderHud(G, frozen ? 0 : dtRaw, now);
  // автосейв
  G._autoT = (G._autoT || 0) + dtRaw;
  if (G._autoT > 45 && !frozen) { G._autoT = 0; save(G); }
  if (G.over && G.over.kind === 'chapter') showChapterEnd(G);
}

function gatherInput() {
  const G = UI.G;
  GAME_INPUT.mx = MOVE.mx + (GAME_INPUT.keyMx || 0);
  GAME_INPUT.my = MOVE.my + (GAME_INPUT.keyMy || 0);
  GAME_INPUT.sneak = MOVE.sneak;
  GAME_INPUT.sprint = MOVE.sprintHold || GAME_INPUT.sprintKey;
  return GAME_INPUT;
}

/* ---------- симуляция ---------- */
function updatePlayerSim(G, dt) {
  if (!G.player.dead) updatePlayer(G, dt, GAME_INPUT);
  // выстрел/удар
  if (GAME_INPUT.attack) {
    GAME_INPUT.attack = false;
    const P = G.player;
    if (P.weapon && ITEM[P.weapon] && ITEM[P.weapon].gun) {
      attack(G, GAME_INPUT.ax || (P.x + Math.cos(P.dir) * 3), GAME_INPUT.ay || (P.y + Math.sin(P.dir) * 3));
      sfx(ITEM[P.weapon].gun.ammo === 'ammo12' ? 'gun12' : ITEM[P.weapon].gun.ammo === 'ammo762' ? 'gun762' : 'gun9');
      CAM.shake = 0.55;
    } else {
      attack(G, GAME_INPUT.ax || (P.x + Math.cos(P.dir) * 2), GAME_INPUT.ay || (P.y + Math.sin(P.dir) * 2));
      sfx('hit');
      CAM.shake = 0.16;
    }
  }
  G._moving = Math.hypot(GAME_INPUT.mx, GAME_INPUT.my) > 0.05;
}

function tickWorld(G, dt, inp) {
  updateZombies(G, dt);
  updateBullets(G, dt);
  updateFlares(G, dt);
  updateProps(G, dt);
  updateGroup(G, dt);
  updateWorld(G, dt);
  updateObjectives(G);
  updateTrust(G, dt);
  dangerSense(G, dt);
  freezeBreath(G, dt);
  if (inp) { inp.danger = false; inp.freeze = false; }
  drainDialog(G);
  // периодические события: нашествие, шум, атмосфера
  G._spawnT = (G._spawnT || 0) + dt;
  if (G._spawnT > (G.spawnEvery || 22)) { G._spawnT = 0; ambientEvent(G); }
  // сброс камеры
  if (CAM.manualT > 0) { CAM.manualT -= dt; if (CAM.manualT <= 0) CAM.follow = true; }
}

/* ---------- фоновое меню ---------- */
let MENU_BG = { t: 0, drops: [], lightning: 0 };
function renderMenuBg(dt) {
  const ctx = RN.ctx;
  if (!ctx) return;
  MENU_BG.t += dt;
  // дождь на фоне
  ctx.fillStyle = '#070809';
  ctx.fillRect(0, 0, RN.W, RN.H);
  // далёкий город
  ctx.fillStyle = '#0e100f';
  for (let i = 0; i < 12; i++) {
    const x = (i * 97 + 20) % RN.W, w = 40 + ((i * 53) % 70), h = 60 + ((i * 71) % 160);
    ctx.fillRect(x, RN.H - h - 40, w, h);
  }
  // огни в окнах
  for (let i = 0; i < 26; i++) {
    const x = (i * 67 + 30) % RN.W, y = RN.H - 60 - ((i * 41) % 150);
    const on = hash2(i, 3, 9) > 0.6 && Math.sin(MENU_BG.t * 0.4 + i) > -0.7;
    if (on) { ctx.fillStyle = i % 3 === 0 ? 'rgba(230,120,60,0.5)' : 'rgba(200,190,140,0.35)'; ctx.fillRect(x, y, 3, 4); }
  }
  // дождь
  ctx.strokeStyle = 'rgba(150,180,200,0.14)';
  ctx.beginPath();
  for (let i = 0; i < 90; i++) {
    const x = ((i * 137 + MENU_BG.t * 220) % (RN.W + 100)) - 50;
    const y = ((i * 71 + MENU_BG.t * 620) % (RN.H + 100)) - 50;
    ctx.moveTo(x, y); ctx.lineTo(x - 4, y + 12);
  }
  ctx.stroke();
  // виньетка
  drawVignette(ctx);
  // вспышка молнии изредка
  if (Math.random() < dt * 0.08) MENU_BG.lightning = 1;
  if (MENU_BG.lightning > 0) {
    MENU_BG.lightning = Math.max(0, MENU_BG.lightning - dt * 4);
    ctx.fillStyle = 'rgba(180,200,220,' + MENU_BG.lightning * 0.10 + ')';
    ctx.fillRect(0, 0, RN.W, RN.H);
  }
}

/* ---------- атмосферные события ---------- */
function ambientEvent(G) {
  const P = G.player, M = G.M, rnd = G.rng;
  const roll = rnd();
  if (roll < 0.34) {
    // далёкая стая: спавн за пределами видимости
    let made = 0;
    for (let i = 0; i < 300 && made < 2 + (G.chapter >> 1); i++) {
      const x = 3 + Math.floor(rnd() * (M.W - 6)), y = 3 + Math.floor(rnd() * (M.H - 6));
      if (Math.hypot(x - P.x, y - P.y) < 16) continue;
      if (!walkable(M, x, y, true)) continue;
      const k = rnd() < 0.25 && G.chapter >= 2 ? 'runner' : 'walker';
      spawnZombieAt(G, k, [x, y]); made++;
    }
    if (made) {
      G.hints.push('Где-то рядом хрипят. Слушай, откуда идёт звук.');
    }
  } else if (roll < 0.55) {
    // скрип/шум
    const a = rnd() * 6.283, d = 6 + rnd() * 8;
    addNoise(G, P.x + Math.cos(a) * d, P.y + Math.sin(a) * d, 8 + rnd() * 6, 'creak');
  } else if (roll < 0.72) {
    // смена погоды
    if (G.weather.rain > 0.6) G.weather.rain -= 0.3; else G.weather.rain += 0.3;
    G.hints.push(G.weather.rain > 0.6 ? 'Дождь заглушает шаги — но холод забирает силы.' : 'Дождь стих. Теперь слышно слишком многое.');
  } else if (roll < 0.86) {
    // находка
    const a = rnd() * 6.283, d = 4 + rnd() * 4;
    const x = clamp(P.x + Math.cos(a) * d, 1, M.W - 2) | 0, y = clamp(P.y + Math.sin(a) * d, 1, M.H - 2) | 0;
    if (walkable(M, x, y)) {
      M.containers.push({ id: M.containers.length, x, y, kind: 'floor', loot: [{ k: pickWeighted(rnd, LOOT_TABLE), n: 1 }], onFloor: true });
      G.hints.push('Что-то блеснуло на земле неподалёку.');
    }
  } else {
    // отдалённый визг
    addNoise(G, P.x + (rnd() - 0.5) * 10, P.y + (rnd() - 0.5) * 10, 12, 'scream');
    sfx('scream');
    G.hints.push('Крикун? Если это он — он позовёт всех.');
  }
}

/* ---------- старт ---------- */
window.addEventListener('load', () => {
  initRenderer($('cv'));
  initInput();
  bootUI();
  // звук включаем по первому касанию
  SND.enabled = true;
  requestAnimationFrame(loop);
});

document.addEventListener('visibilitychange', () => {
  if (document.hidden) { if (UI.G) { save(UI.G); UI.G.paused = true; } stopAmbient(); }
  else { if (UI.G && !UI.screen) UI.G.paused = false; if (UI.G) startAmbient(UI.G); }
});

document.addEventListener('contextmenu', e => e.preventDefault());
document.addEventListener('gesturestart', e => e.preventDefault());
