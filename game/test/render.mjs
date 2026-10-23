// Render real frames to PNG for visual inspection (uses @napi-rs/canvas).
import { createCanvas } from '@napi-rs/canvas';
import { mkdir, writeFile } from 'node:fs/promises';

const noop = () => {};
const listeners = {};
const realCanvas = createCanvas(512, 288);
realCanvas.style = {};
realCanvas.addEventListener = noop;
realCanvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 512, height: 288, right: 512, bottom: 288 });
const wrap = { style: {}, appendChild: noop };

globalThis.window = {
  innerWidth: 1440, innerHeight: 900,
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  removeEventListener: noop,
};
globalThis.document = {
  getElementById: (id) => (id === 'game' ? realCanvas : id === 'wrap' ? wrap : null),
  createElement: (t) => {
    if (t !== 'canvas') return { style: {}, remove: noop };
    const c = createCanvas(512, 288);
    c.addEventListener = noop;
    return c;
  },
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  body: { appendChild: noop },
};
globalThis.performance = { now: () => Date.now() };
globalThis.requestAnimationFrame = noop;

await import(new URL('../src/main.js', import.meta.url).href);
for (const f of listeners['DOMContentLoaded'] || []) f();
const game = globalThis.window.__game;

await mkdir(new URL('../shots/', import.meta.url), { recursive: true });
const out = (name) => writeFile(new URL('../shots/' + name, import.meta.url), realCanvas.toBuffer('image/png'));
// 2x nearest-neighbour upscale for review
const up = async (from, to) => {
  const { loadImage } = await import('@napi-rs/canvas');
  const img = await loadImage(new URL('../shots/' + from, import.meta.url));
  const c = createCanvas(1024, 576);
  const g = c.getContext('2d');
  g.imageSmoothingEnabled = false;
  g.drawImage(img, 0, 0, 1024, 576);
  await writeFile(new URL('../shots/' + to, import.meta.url), c.toBuffer('image/png'));
};

const dt = 1 / 60;
// staged shots: the player is untouchable so the scene stays readable
game.player.applyHit = () => {};
const freeze = (on) => {
  for (const e of game.entities) {
    if (e.isPlayer) continue;
    e.ai.frozen = on;
    if (on) { e.ai.target = null; e.ai.retarget = 1e9; e.ai.state = 'idle'; }
    else e.ai.retarget = 0;
  }
};
freeze(true);
function step(sec, fn) {
  const n = Math.round(sec * 60);
  for (let i = 0; i < n; i++) {
    if (fn) fn(i);
    game.player.health = 100;
    game.player.blood = 5000;
    game.update(dt);
  }
  game.render();
}

// 1) title screen
game.render();
await out('01-start.png');

// 2) staged scene: four people standing
game.started = true;
const npcA = game.entities[1];
const npcB = game.entities[2];
const npcC = game.entities[3];
const layout = [[npcA, 360, 'machete'], [npcB, 430, 'smg'], [npcC, 150, 'axe']];
for (const [e, x, w] of layout) { e.pos.x = x; e.setWeapon(w); e.ai.target = null; e.ai.retarget = 999; }
game.player.pos.x = 240;
game.player.setWeapon('pistol');
game.mouse.x = 380; game.mouse.y = 210;
step(0.7);
await out('02-idle.png');
await up('02-idle.png', '02-idle@2x.png');

// 3) pistol shot at the machete guy
game.player.setWeapon('pistol');
step(0.5, (i) => {
  game.mouse.x = npcA.pts.chest.x; game.mouse.y = npcA.pts.chest.y;
  game.input.mouse.down = true;
});
await out('03-shooting.png');

step(1.2, () => { game.mouse.x = npcA.pts.chest.x; game.mouse.y = npcA.pts.chest.y; game.input.mouse.down = true; });
game.input.mouse.down = false;
step(0.5);
await out('04-after-shots.png');

// 4) shotgun point blank
const victim = game.spawnNPC(330, 'knife');
victim.ai.target = null; victim.ai.retarget = 999;
game.player.pos.x = 250;
game.player.setWeapon('shotgun');
step(0.2, () => { game.mouse.x = victim.pts.chest.x; game.mouse.y = victim.pts.chest.y; game.input.mouse.down = true; });
game.input.mouse.down = false;
step(0.28);
await out('05-shotgun.png');
await up('05-shotgun.png', '05-shotgun@2x.png');

// 5) katana dismemberment
const victim2 = game.spawnNPC(300, 'knife');
victim2.ai.target = null; victim2.ai.retarget = 999;
game.player.pos.x = 250;
game.player.setWeapon('katana');
for (let k = 0; k < 10; k++) {
  step(0.5, () => {
    game.mouse.x = victim2.pts.chest.x + 3;
    game.mouse.y = victim2.pts.chest.y;
    game.input.mouse.down = true;
  });
  game.input.mouse.down = false;
  step(0.12);
  if (victim2.limbs.filter((l) => l.severed).length >= 2) break;
}
step(0.35);
await out('06-dismember.png');
await up('06-dismember.png', '06-dismember@2x.png');

// 6) aftermath pool of blood
step(2.6);
await out('07-aftermath.png');
await up('07-aftermath.png', '07-aftermath@2x.png');

// 7) chaotic brawl
freeze(false);
for (const e of game.entities) { if (!e.isPlayer) { e.ai.target = null; e.ai.retarget = 0; } }
step(9);
await out('08-brawl.png');

// 8) death screen
game.player.die();
game.player.deathCause = 'обезглавлен';
step(1.4);
await out('09-death.png');

console.log('rendered');
console.log('victim2 severed:', victim2.limbs.filter((l) => l.severed).map((l) => l.label).join(', ') || 'none');
console.log('debris:', game.debris.length, 'decals:', game.gore.pools);
process.exit(0);
