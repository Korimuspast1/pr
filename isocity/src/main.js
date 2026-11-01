import { Sim, COSTS, CAPITAL } from './sim.js?v=2026-11-01-pixel-6';
import { Renderer, TOOLS } from './render.js?v=2026-11-01-pixel-6';

export const BUILD = 'pixel-6';

const canvas = document.getElementById('city');
const sim = new Sim(7);
const renderer = new Renderer(canvas, sim);
renderer.build = BUILD;
console.info(`[город] сборка ${BUILD}: пиксель, чёткий HUD, въезд с края карты`);

let last = performance.now();
let action = null; // { kind: 'paint' | 'pan', tool, lastTile, startX, startY }
let hoverTile = null;
let keys = new Set();

// Keyboard handling that survives Caps Lock, Shift and a Russian layout:
// e.code gives KeyW/KeyA/KeyS/KeyD no matter what is printed on the key,
// and e.key is lower-cased so "W" and "w" are the same.
function keyName(e) {
  if (e.code && /^Key[A-Z]$/.test(e.code)) return e.code.slice(3).toLowerCase();
  if (e.code && e.code.startsWith('Arrow')) return e.code;
  return (e.key || '').toLowerCase();
}

function held(...names) {
  for (const n of names) if (keys.has(n)) return true;
  return false;
}
let hintShown = false;

// ------------------------------------------------------------------ helpers
function bufferPoint(e) {
  const rect = canvas.getBoundingClientRect();
  const cssX = e.clientX - rect.left;
  const cssY = e.clientY - rect.top;
  return {
    cssX,
    cssY,
    bx: cssX / renderer.pxScale,
    by: cssY / renderer.pxScale,
  };
}

function applyTool(tile) {
  if (!tile) return;
  const tool = renderer.tool;
  let res = { ok: true };
  if (tool === 'road') res = sim.buildRoad(tile.x, tile.y);
  else if (tool === 'park') res = sim.buildPark(tile.x, tile.y);
  else if (tool === 'bulldoze') res = sim.bulldoze(tile.x, tile.y);
  else if (tool === 'zoneR') res = sim.zone(tile.x, tile.y, 'r');
  else if (tool === 'zoneC') res = sim.zone(tile.x, tile.y, 'c');
  else if (tool === 'zoneI') res = sim.zone(tile.x, tile.y, 'i');
  if (res && !res.ok && res.reason) renderer.toastMessage(res.reason, true);
  else if (res && res.ok && !sim.townConnected() && !hintShown) {
    hintShown = true;
    renderer.toastMessage('дорога не доходит до края карты — никто не приедет', true);
  }
}

function paintTo(tile) {
  if (!tile) return;
  const prev = action?.lastTile;
  if (prev) {
    // fill the gap so a fast mouse drag does not leave holes
    let x0 = prev.x;
    let y0 = prev.y;
    const x1 = tile.x;
    const y1 = tile.y;
    const dx = Math.abs(x1 - x0);
    const dy = Math.abs(y1 - y0);
    const sx = x0 < x1 ? 1 : -1;
    const sy = y0 < y1 ? 1 : -1;
    let err = dx - dy;
    let guard = 0;
    while (guard++ < 200) {
      applyTool({ x: x0, y: y0 });
      if (x0 === x1 && y0 === y1) break;
      const e2 = 2 * err;
      if (e2 > -dy) { err -= dy; x0 += sx; }
      if (e2 < dx) { err += dx; y0 += sy; }
    }
  } else {
    applyTool(tile);
  }
  action.lastTile = { x: tile.x, y: tile.y };
}

// -------------------------------------------------------------------- input
canvas.addEventListener('contextmenu', (e) => e.preventDefault());

canvas.addEventListener('pointerdown', (e) => {
  canvas.setPointerCapture?.(e.pointerId);
  const p = bufferPoint(e);
  const hit = renderer.hudHit(p.cssX, p.cssY);
  if (hit) {
    if (hit.kind === 'tool') renderer.tool = hit.id;
    if (hit.kind === 'summary') renderer.summaryOpen = !renderer.summaryOpen;
    if (hit.kind === 'speed') {
      if (hit.v === 0) sim.paused = !sim.paused;
      else {
        sim.paused = false;
        sim.speed = hit.v;
      }
    }
    return;
  }
  const tile = renderer.tileAt(p.cssX, p.cssY);
  if (e.button === 1 || (e.button === 0 && (e.shiftKey || keys.has(' ')))) {
    action = { kind: 'pan', startCss: { x: e.clientX, y: e.clientY }, cam: { ...renderer.cam } };
    return;
  }
  if (e.button === 2) {
    const prev = renderer.tool;
    renderer.tool = 'bulldoze';
    action = { kind: 'paint', tool: 'bulldoze', lastTile: null, restore: prev };
    paintTo(tile);
    return;
  }
  if (e.button === 0) {
    action = { kind: 'paint', tool: renderer.tool, lastTile: null };
    paintTo(tile);
  }
});

canvas.addEventListener('pointermove', (e) => {
  const p = bufferPoint(e);
  hoverTile = renderer.tileAt(p.cssX, p.cssY);
  renderer.hover = hoverTile;
  if (!action) return;
  if (action.kind === 'pan') {
    renderer.cam.x = action.cam.x - (e.clientX - action.startCss.x) / renderer.pxScale;
    renderer.cam.y = action.cam.y - (e.clientY - action.startCss.y) / renderer.pxScale;
    renderer.clampCamera();
    return;
  }
  if (action.kind === 'paint') {
    if (action.tool !== renderer.tool && action.tool !== 'bulldoze') return;
    paintTo(renderer.tileAt(p.cssX, p.cssY));
  }
});

function endAction(e) {
  if (action?.restore) renderer.tool = action.restore;
  action = null;
  if (e) canvas.releasePointerCapture?.(e.pointerId);
}
canvas.addEventListener('pointerup', endAction);
canvas.addEventListener('pointercancel', endAction);
canvas.addEventListener('pointerleave', () => {
  renderer.hover = null;
});

canvas.addEventListener(
  'wheel',
  (e) => {
    e.preventDefault();
    const p = bufferPoint(e);
    const before = renderer.screenToWorld(p.cssX, p.cssY);
    renderer.setZoom(renderer.zoom + (e.deltaY < 0 ? 1 : -1), null);
    const after = renderer.screenToWorld(p.cssX, p.cssY);
    renderer.cam.x += before.wx - after.wx;
    renderer.cam.y += before.wy - after.wy;
    renderer.clampCamera();
  },
  { passive: false },
);

// touch: two fingers pan, pinch changes zoom, one finger paints
let touch = null;
canvas.addEventListener(
  'touchstart',
  (e) => {
    if (e.touches.length === 2) {
      const [a, b] = e.touches;
      touch = { dist: Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY), cx: (a.clientX + b.clientX) / 2, cy: (a.clientY + b.clientY) / 2 };
      action = null;
    }
  },
  { passive: true },
);
canvas.addEventListener(
  'touchmove',
  (e) => {
    if (e.touches.length === 2 && touch) {
      e.preventDefault();
      const [a, b] = e.touches;
      const dist = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
      if (Math.abs(dist - touch.dist) > 24) {
        renderer.setZoom(renderer.zoom + (dist > touch.dist ? 1 : -1), null);
        touch.dist = dist;
      }
      const cx = (a.clientX + b.clientX) / 2;
      const cy = (a.clientY + b.clientY) / 2;
      renderer.panBy(-(cx - touch.cx) / renderer.pxScale, -(cy - touch.cy) / renderer.pxScale);
      touch.cx = cx;
      touch.cy = cy;
    }
  },
  { passive: false },
);
canvas.addEventListener('touchend', () => { touch = null; });

window.addEventListener('keydown', (e) => {
  const name = keyName(e);
  keys.add(name);
  keys.add((e.key || '').toLowerCase());
  if (name === ' ') e.preventDefault();
  const key = (e.key || '').toLowerCase();
  const toolIndex = TOOLS.findIndex((t) => t.key === key);
  if (toolIndex >= 0 && key !== '') renderer.tool = TOOLS[toolIndex].id;
  if (key === 'p' || key === 'з') sim.paused = !sim.paused;
  if (key === '+' || key === '=' || key === ']') sim.speed = Math.min(8, sim.speed * 2);
  if (key === '-' || key === '_' || key === '[') sim.speed = Math.max(1, sim.speed / 2);
  if (key === 'e' || key === 'у') renderer.summaryOpen = !renderer.summaryOpen;
  if (key === 'Tab') {
    e.preventDefault();
    renderer.showHud = !renderer.showHud;
  }
  if (key === 'h' || key === 'р') renderer.showHud = !renderer.showHud;
});

window.addEventListener('keyup', (e) => {
  keys.delete(keyName(e));
  keys.delete((e.key || '').toLowerCase());
  keys.delete(e.key);
});
window.addEventListener('blur', () => keys.clear());

window.addEventListener('resize', () => renderer.resize());

// camera keys: WASD in any layout, arrows as well
function cameraKeys(dt) {
  const speed = 280 * dt;
  if (held('ArrowLeft', 'a', 'ф')) renderer.panBy(-speed, 0);
  if (held('ArrowRight', 'd', 'в')) renderer.panBy(speed, 0);
  if (held('ArrowUp', 'w', 'ц')) renderer.panBy(0, -speed * 0.75);
  if (held('ArrowDown', 's', 'ы')) renderer.panBy(0, speed * 0.75);
}

// ------------------------------------------------- tell the player about money
// The simulation queues important events (a house is being built, someone
// arrived, someone left) and the UI turns them into short messages. That is the
// answer to "where did my money go" without opening any menu.
const KIND_NAMES = { house: 'дом', shop: 'лавка', factory: 'завод', park: 'парк', cityhall: 'ратуша' };
function watchEvents() {
  if (!sim.events.length) return;
  const events = sim.events.splice(0, sim.events.length);
  for (const ev of events) {
    if (ev.type === 'build') renderer.toastMessage(`стройка: ${KIND_NAMES[ev.kind] || ev.kind} · −${ev.price} ₽ из бюджета`);
    else if (ev.type === 'arrive') renderer.toastMessage(`${ev.name} переехал(а) в город · +${Math.round(ev.cash)} ₽`);
    else if (ev.type === 'leave') renderer.toastMessage(`${ev.name} уехал(а) из города · −${Math.round(ev.cash)} ₽`, true);
  }
}

// --------------------------------------------------------------------- loop
let acc = 0;
function frame(now) {
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  acc += dt;
  cameraKeys(dt);
  sim.update(dt);
  watchEvents();
  renderer.update(dt);
  renderer.draw();
  requestAnimationFrame(frame);
}

renderer.toastMessage('дорога до края карты → зоны → люди приедут сами · E — экономика города');
requestAnimationFrame(frame);

// handy for debugging and for the headless smoke test
window.city = { sim, renderer, applyTool };
