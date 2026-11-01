import { Sim } from './economy.js';
import { Renderer } from './render.js';
import { drawOverlay } from './overlay.js';

const canvas = document.getElementById('city');
const sim = new Sim(11);
const renderer = new Renderer(canvas, sim);

let showOverlay = false;
let dragging = null;
let last = performance.now();

// --- camera ---------------------------------------------------------------
canvas.addEventListener('pointerdown', (e) => {
  canvas.setPointerCapture(e.pointerId);
  dragging = { x: e.clientX, y: e.clientY, panX: renderer.pan.x, panY: renderer.pan.y };
  renderer.autoFit = false;
});

canvas.addEventListener('pointermove', (e) => {
  if (!dragging) return;
  const z = renderer.zoom;
  renderer.pan.x = dragging.panX - (e.clientX - dragging.x) / z;
  renderer.pan.y = dragging.panY - (e.clientY - dragging.y) / z;
});

const endDrag = (e) => {
  if (dragging) canvas.releasePointerCapture?.(e.pointerId);
  dragging = null;
};
canvas.addEventListener('pointerup', endDrag);
canvas.addEventListener('pointercancel', endDrag);

canvas.addEventListener(
  'wheel',
  (e) => {
    e.preventDefault();
    const before = renderer.screenToWorld(e.clientX, e.clientY);
    const factor = Math.exp(-e.deltaY * 0.0012);
    renderer.zoom = Math.min(3, Math.max(0.28, renderer.zoom * factor));
    const after = renderer.screenToWorld(e.clientX, e.clientY);
    renderer.pan.x += before.x - after.x;
    renderer.pan.y += before.y - after.y;
    renderer.autoFit = false;
  },
  { passive: false },
);

// pinch on touch screens
let pinch = null;
canvas.addEventListener(
  'touchmove',
  (e) => {
    if (e.touches.length !== 2) return;
    e.preventDefault();
    const [a, b] = e.touches;
    const dist = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
    if (pinch) {
      renderer.zoom = Math.min(3, Math.max(0.28, renderer.zoom * (dist / pinch)));
      renderer.autoFit = false;
    }
    pinch = dist;
  },
  { passive: false },
);
canvas.addEventListener('touchend', () => { pinch = null; });

window.addEventListener('keydown', (e) => {
  if (e.key === 'h' || e.key === 'H' || e.key === 'р' || e.key === 'Р') showOverlay = !showOverlay;
  if (e.key === 'f' || e.key === 'F' || e.key === 'а' || e.key === 'А') renderer.fit();
  if (e.key === ' ') {
    e.preventDefault();
    sim.paused = !sim.paused;
  }
  if (e.key === '1') sim.setSpeed(1);
  if (e.key === '2') sim.setSpeed(2);
  if (e.key === '3') sim.setSpeed(4);
  if (e.key === '4') sim.setSpeed(8);
});

window.addEventListener('resize', () => {
  renderer.resize();
  if (renderer.autoFit) renderer.fit();
});

// --- loop -----------------------------------------------------------------
function frame(now) {
  const dt = Math.min((now - last) / 1000, 0.1);
  last = now;
  sim.update(dt);
  renderer.update(dt);
  renderer.draw();
  if (showOverlay) drawOverlay(renderer.ctx, renderer, sim, renderer.w, renderer.h);
  requestAnimationFrame(frame);
}

renderer.hint = 9;
requestAnimationFrame(frame);

// expose for debugging from the console
window.city = { sim, renderer };
