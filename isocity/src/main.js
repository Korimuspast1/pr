import { Sim } from './sim.js?v=2026-11-01-pixel-3';
import { Renderer, TOOLS } from './render.js?v=2026-11-01-pixel-3';

export const BUILD = 'pixel-3';

const canvas = document.getElementById('city');
const sim = new Sim(7);
const renderer = new Renderer(canvas, sim);
renderer.build = BUILD;
console.info(`[город] сборка ${BUILD}: пиксель, зоны, стройка`);

let last = performance.now();
let action = null; // { kind: 'paint' | 'pan', tool, lastTile, startX, startY }
let hoverTile = null;
let keys = new Set();

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

function hudHit(bx, by) {
  const W = renderer.bufW;
  const H = renderer.bufH;
  // speed buttons
  if (by >= 4 && by <= 20) {
    const bw = 16;
    const startX = W - 4 - 4 * (bw + 2) + 2;
    for (let i = 0; i < 4; i++) {
      const x = startX + i * (bw + 2);
      if (bx >= x && bx <= x + bw) return { kind: 'speed', v: [0, 1, 2, 4][i] };
    }
  }
  // toolbar
  const cell = 26;
  const gap = 2;
  const totalW = TOOLS.length * (cell + gap) - gap;
  const tx = Math.round((W - totalW) / 2);
  const ty = H - cell - 6;
  if (by >= ty - 4 && by <= ty + cell + 4 && bx >= tx - 4 && bx <= tx + totalW + 4) {
    for (let i = 0; i < TOOLS.length; i++) {
      const x = tx + i * (cell + gap);
      if (bx >= x && bx <= x + cell) return { kind: 'tool', id: TOOLS[i].id };
    }
  }
  return null;
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
  const hit = renderer.showHud ? hudHit(p.bx, p.by) : null;
  if (hit) {
    if (hit.kind === 'tool') renderer.tool = hit.id;
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
  keys.add(e.key);
  if (e.key === ' ') e.preventDefault();
  const toolIndex = TOOLS.findIndex((t) => t.key === e.key);
  if (toolIndex >= 0) renderer.tool = TOOLS[toolIndex].id;
  if (e.key === 'p' || e.key === 'P' || e.key === 'з') sim.paused = !sim.paused;
  if (e.key === '+' || e.key === '=') sim.speed = Math.min(8, sim.speed * 2);
  if (e.key === '-' || e.key === '_') sim.speed = Math.max(1, sim.speed / 2);
  if (e.key === 'Tab') {
    e.preventDefault();
    renderer.showHud = !renderer.showHud;
  }
  if (e.key === 'h' || e.key === 'H' || e.key === 'р') renderer.showHud = !renderer.showHud;
});

window.addEventListener('keyup', (e) => keys.delete(e.key));
window.addEventListener('blur', () => keys.clear());

window.addEventListener('resize', () => renderer.resize());

// camera keys
function cameraKeys(dt) {
  const speed = 260 * dt;
  if (keys.has('ArrowLeft') || keys.has('a') || keys.has('ф')) renderer.panBy(-speed, 0);
  if (keys.has('ArrowRight') || keys.has('d') || keys.has('в')) renderer.panBy(speed, 0);
  if (keys.has('ArrowUp') || keys.has('w') || keys.has('ц')) renderer.panBy(0, -speed * 0.7);
  if (keys.has('ArrowDown') || keys.has('s') || keys.has('ы')) renderer.panBy(0, speed * 0.7);
}

// --------------------------------------------------------------------- loop
let acc = 0;
function frame(now) {
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  acc += dt;
  cameraKeys(dt);
  sim.update(dt);
  renderer.update(dt);
  renderer.draw();
  requestAnimationFrame(frame);
}

renderer.toastMessage('стрите город: дорога → зоны → люди приедут сами');
requestAnimationFrame(frame);

// handy for debugging and for the headless smoke test
window.city = { sim, renderer, applyTool };
