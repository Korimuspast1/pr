// Headless smoke test: stubs the DOM/canvas and runs the real game loop.
// Usage: node test/headless.mjs

const noop = () => {};
const gradient = { addColorStop: noop };

function makeCtx() {
  const ctx = {
    canvas: null,
    globalAlpha: 1, fillStyle: '#000', strokeStyle: '#000', lineWidth: 1,
    font: '', textAlign: 'left', textBaseline: 'top', imageSmoothingEnabled: false, lineCap: 'butt',
    createLinearGradient: () => gradient,
    createRadialGradient: () => gradient,
    createPattern: () => null,
    measureText: () => ({ width: 10 }),
    getImageData: (x, y, w, h) => ({ data: new Uint8ClampedArray(Math.max(1, w * h * 4)) }),
    putImageData: noop,
    drawImage: noop,
    setTransform: noop,
    save: noop, restore: noop, translate: noop, rotate: noop, scale: noop,
    beginPath: noop, closePath: noop, moveTo: noop, lineTo: noop, arc: noop, ellipse: noop,
    rect: noop, fill: noop, stroke: noop, fillRect: noop, strokeRect: noop, clearRect: noop,
    fillText: noop, strokeText: noop, clip: noop, quadraticCurveTo: noop, bezierCurveTo: noop,
  };
  return ctx;
}

function makeCanvas(w = 640, h = 360) {
  const el = {
    width: w, height: h, style: {},
    getContext: () => { const c = makeCtx(); c.canvas = el; return c; },
    addEventListener: noop, removeEventListener: noop,
    getBoundingClientRect: () => ({ left: 0, top: 0, width: w, height: h, right: w, bottom: h }),
  };
  return el;
}

const listeners = {};
const gameCanvas = makeCanvas();
const wrap = { style: {}, appendChild: noop };

globalThis.window = {
  innerWidth: 1280, innerHeight: 800, devicePixelRatio: 2,
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  removeEventListener: noop,
  AudioContext: undefined,
  requestAnimationFrame: noop,
};
globalThis.document = {
  getElementById: (id) => (id === 'game' ? gameCanvas : id === 'wrap' ? wrap : null),
  createElement: (t) => (t === 'canvas' ? makeCanvas() : { style: {}, remove: noop }),
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  body: { appendChild: noop },
};
globalThis.performance = { now: () => Date.now() };
globalThis.requestAnimationFrame = noop;
globalThis.cancelAnimationFrame = noop;

await import(new URL('../src/main.js', import.meta.url).href);
for (const f of listeners['DOMContentLoaded'] || []) f();

const game = globalThis.window.__game;
if (!game) throw new Error('game failed to boot');

const errors = [];
function step(seconds, opts = {}) {
  const dt = 1 / 60;
  for (let i = 0; i < Math.round(seconds * 60); i++) {
    game.time += 0; // let update advance it
    if (opts.mouse) game.mouse.x = opts.mouse[i % opts.mouse.length];
    if (opts.down != null) game.input.mouse.down = opts.down;
    try {
      game.update(dt);
      game.render();
    } catch (e) {
      errors.push(e);
      console.error('FRAME ERROR at t=' + game.time.toFixed(2), e);
      break;
    }
  }
}

game.started = true;
game.input.keys.add('KeyD');
step(1.2);
game.input.keys.delete('KeyD');
console.log('player x after walk:', game.player.pos.x.toFixed(1));

// Shoot some rounds at an NPC
game.player.setWeapon('pistol');
const npc = game.entities.find((e) => e !== game.player);
game.mouse.x = npc.pts.chest.x;
game.mouse.y = npc.pts.chest.y;
step(2.5, { down: true });
console.log('bullets alive:', game.combat.bullets.length, 'npc hp:', npc.health.toFixed(1), 'blood:', npc.blood.toFixed(0));
console.log('gore parts:', game.gore.parts.length, 'chunks:', game.gore.chunks.length, 'pools:', game.gore.pools);

// Melee dismemberment check: whack a fresh NPC with a katana until something comes off
const victim = game.spawnNPC(300, 'knife');
victim.ai.target = null;
game.player.setWeapon('katana');
let severed = 0;
for (let k = 0; k < 26; k++) {
  game.mouse.x = victim.pts.chest.x;
  game.mouse.y = victim.pts.chest.y;
  game.input.mouse.down = true;
  step(0.36, { down: true });
  game.input.mouse.down = false;
  step(0.1);
  severed = victim.limbs.filter((l) => l.severed).length;
  if (severed) break;
}
console.log('victim severed limbs:', severed, 'alive:', victim.alive, 'debris:', game.debris.length);
console.log('victim hp:', victim.health.toFixed(1), 'blood:', victim.blood.toFixed(0), 'bleedRate:', victim.bleedTotal.toFixed(2));

// Let the sandbox run: NPCs fight each other
game.entities.forEach((e) => { if (!e.isPlayer) e.ai.retarget = 0; });
step(30);
console.log('after 30s — alive:', game.entities.filter((e) => e.alive).length, 'corpses:', game.entities.filter((e) => !e.alive).length);
console.log('gore parts:', game.gore.parts.length, 'chunks:', game.gore.chunks.length, 'casings:', game.gore.casings.length, 'pools:', game.gore.pools);

// sanity
function scan(obj, path = 'game', depth = 0, seen = new Set()) {
  if (depth > 4 || !obj || typeof obj !== 'object' || seen.has(obj)) return;
  seen.add(obj);
  for (const k of Object.keys(obj)) {
    const v = obj[k];
    if (typeof v === 'number' && !Number.isFinite(v)) {
      console.error('NaN/Inf at', path + '.' + k, v);
      errors.push(new Error('NaN at ' + path + '.' + k));
    }
    if (v && typeof v === 'object' && !(v instanceof Set)) scan(v, path + '.' + k, depth + 1, seen);
  }
}
scan(game.player, 'player');
scan(game.entities, 'entities', 0);
scan(game.debris, 'debris', 0);

console.log('player health:', game.player.health.toFixed(1), 'blood:', game.player.blood.toFixed(0), 'alive:', game.player.alive);
console.log(errors.length ? `FAILED with ${errors.length} error(s)` : 'OK — no runtime errors');
process.exit(errors.length ? 1 : 0);
