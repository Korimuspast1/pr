import { Game, TOOLS } from './game.js';
import { UI } from './ui.js';
import { Renderer } from './render.js';
import { AudioEngine } from './audio.js';

const canvas = document.getElementById('view');
const audio = new AudioEngine();

function resize() {
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  const w = Math.floor(window.innerWidth * dpr);
  const h = Math.floor(window.innerHeight * dpr);
  const oldW = canvas.width, oldH = canvas.height;
  canvas.width = w;
  canvas.height = h;
  canvas.style.width = window.innerWidth + 'px';
  canvas.style.height = window.innerHeight + 'px';
  if (game && oldW && oldH) {
    game.cam.x += (oldW - w) / (2 * game.cam.zoom);
    game.cam.y += (oldH - h) / (2 * game.cam.zoom);
    game.clampCamera();
  }
}

let game = null;
let ui = null;
let renderer = null;

resize();
const uiStub = {
  drawCursor: () => {},
  toast: () => {},
  onMapLoaded: () => {},
  onToolChanged: () => {},
  setStats: () => {},
};
game = new Game(canvas, audio, uiStub);
ui = new UI(game, audio);
game.ui = ui;
renderer = new Renderer(game);

window.addEventListener('resize', resize);

// ---------------------------------------------------------------- interaction
canvas.addEventListener('contextmenu', (e) => e.preventDefault());

canvas.addEventListener('pointerdown', (e) => {
  audio.init();
  canvas.setPointerCapture?.(e.pointerId);
  game.pointerDown(e);
  if (ui.consoleOpen) ui.toggleConsole(false);
});

window.addEventListener('pointermove', (e) => {
  game.pointerMove(e);
});

window.addEventListener('pointerup', () => game.pointerUp());
window.addEventListener('pointercancel', () => game.pointerUp());

canvas.addEventListener('wheel', (e) => {
  e.preventDefault();
  game.wheel(e);
}, { passive: false });

// touch: two fingers pan, pinch zoom
let touchState = null;
canvas.addEventListener('touchstart', (e) => {
  if (e.touches.length === 2) {
    const [a, b] = e.touches;
    touchState = {
      dist: Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY),
      cx: (a.clientX + b.clientX) / 2,
      cy: (a.clientY + b.clientY) / 2,
      camX: game.cam.x,
      camY: game.cam.y,
      zoom: game.cam.zoom,
    };
  }
}, { passive: true });

canvas.addEventListener('touchmove', (e) => {
  if (e.touches.length === 2 && touchState) {
    e.preventDefault();
    const [a, b] = e.touches;
    const dist = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
    const cx = (a.clientX + b.clientX) / 2;
    const cy = (a.clientY + b.clientY) / 2;
    const before = game.screenToWorld(cx, cy);
    game.cam.zoom = Math.min(1.4, Math.max(0.1, touchState.zoom * (dist / touchState.dist)));
    game.cam.x = before.x - (cx - canvas.getBoundingClientRect().left) / game.cam.zoom;
    game.cam.y = before.y - (cy - canvas.getBoundingClientRect().top) / game.cam.zoom;
    game.cam.x -= (cx - touchState.cx) / game.cam.zoom;
    game.cam.y -= (cy - touchState.cy) / game.cam.zoom;
    game.clampCamera();
  }
}, { passive: false });

canvas.addEventListener('touchend', () => { touchState = null; }, { passive: true });

// ------------------------------------------------------------------- keyboard
const toolByKey = {};
for (const t of TOOLS) toolByKey[t.key.toLowerCase()] = t.id;

window.addEventListener('keydown', (e) => {
  if (ui.consoleOpen && e.target.tagName === 'INPUT') return;
  if (e.target.tagName === 'INPUT') return;
  const k = e.key.toLowerCase();
  audio.init();
  game.keys.add(k);
  if (k === ' ') game.spaceDown = true;
  if (toolByKey[k]) { game.setTool(toolByKey[k]); return; }
  switch (k) {
    case 'h': ui.toggleUI(); break;
    case 't':
      game.slowmo = !game.slowmo;
      game.timeScale = game.slowmo ? 0.25 : 1;
      ui.toast(game.lang === 'ru' ? (game.slowmo ? 'Замедление ×0.25' : 'Скорость нормальная') : (game.slowmo ? 'Slow motion ×0.25' : 'Normal speed'));
      break;
    case 'p': ui.togglePause(); break;
    case 'r': game.setMap(game.mapName); break;
    case 'f': ui.toggleFullscreen(); break;
    case '~': case '`':
      e.preventDefault();
      ui.toggleConsole();
      break;
    case 'escape':
      if (ui.consoleOpen) ui.toggleConsole(false);
      else if (game.state === 'play') ui.togglePause(true);
      break;
    default: break;
  }
});

window.addEventListener('keyup', (e) => {
  const k = e.key.toLowerCase();
  game.keys.delete(k);
  if (k === ' ') game.spaceDown = false;
});

// ---------------------------------------------------------------- the loop
let last = performance.now();
let statsTimer = 0;
let fpsAcc = 0;
let fpsCount = 0;
let fps = 60;
let avgFrame = 1 / 60;

function frame(now) {
  const dt = Math.min(0.1, (now - last) / 1000);
  last = now;
  fpsAcc += dt;
  fpsCount++;
  if (fpsAcc > 0.5) { fps = Math.round(fpsCount / fpsAcc); fpsAcc = 0; fpsCount = 0; }

  game.update(dt);
  renderer.draw();

  // adaptive quality: keep the framerate up during mass destruction
  avgFrame = avgFrame * 0.92 + dt * 0.08;
  if (!game.lowQ && avgFrame > 0.026) game.lowQ = true;
  else if (game.lowQ && avgFrame < 0.017) game.lowQ = false;

  statsTimer += dt;
  if (statsTimer > 0.25) {
    statsTimer = 0;
    ui.setStats();
    if (game.fallout > 0) {
      const w = game.world;
      const cx = Math.floor(game.centerX() / game.cell);
      const cy = Math.floor((game.cam.y + 100) / game.cell);
      audio.geiger(w.inB(cx, cy) ? w.rad[w.idx(cx, cy)] * 3 : 0.4);
    }
  }
  requestAnimationFrame(frame);
}

requestAnimationFrame(frame);

// expose for debugging / the built-in console
window.WRECKBOX = { game, ui, renderer, audio, get fps() { return fps; } };

// the game starts behind the title screen; the loop already renders the city
game.state = 'menu';
